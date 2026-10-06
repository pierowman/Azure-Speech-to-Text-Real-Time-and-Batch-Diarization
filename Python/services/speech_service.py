"""
Azure Speech-to-Text service with diarization support
"""
from __future__ import annotations

import logging
import json
import time
from typing import Optional
from models import TranscriptionResult, SpeakerSegment, SpeakerInfo
from exceptions import TranscriptionException, AzureServiceException
from config import config

logger = logging.getLogger(__name__)


class _ScopedTokenCredential:
    """Wrap a TokenCredential to force the Cognitive Services token scope.

    The Speech SDK always requests the hard-coded commercial scope
    ``https://cognitiveservices.azure.com/.default`` when it is handed a
    ``token_credential`` (see ``_Constants.TokenRequestScopes`` in the SDK).
    In sovereign clouds such as Azure US Government, the Speech endpoint only
    accepts tokens issued for that cloud's audience
    (e.g. ``https://cognitiveservices.azure.us/.default``). A commercial-audience
    token is rejected by the Gov endpoint, which surfaces as a silent cancel with
    zero recognized segments. This wrapper ignores the scope requested by the SDK
    and substitutes the correct cloud-aware scope. For commercial the scope is
    identical, so this is a no-op there.
    """

    def __init__(self, inner, scope: str):
        self._inner = inner
        self._scope = scope

    def get_token(self, *scopes, **kwargs):
        return self._inner.get_token(self._scope, **kwargs)


class SpeechToTextService:
    """Service for real-time speech transcription with diarization"""
    
    def __init__(self):
        self.region = config.AZURE_SPEECH_REGION
        # Real-time connects to the region-based WebSocket host, built cloud-aware
        # from the region and Azure cloud (commercial vs US Government). The SDK
        # cannot reliably derive this from the custom domain in sovereign clouds.
        self.endpoint = config.REALTIME_SPEECH_ENDPOINT
        self.resource_id = config.AZURE_SPEECH_RESOURCE_ID
        self.default_locale = config.DEFAULT_LOCALE
        self.poll_interval = config.TRANSCRIPTION_POLL_INTERVAL_SECONDS
        self.max_transcription_attempts = max(1, config.TRANSCRIPTION_MAX_ATTEMPTS)
        self.transcription_retry_delay_seconds = config.TRANSCRIPTION_RETRY_DELAY_SECONDS

        if not self.region:
            raise ValueError("Azure Speech region not found in configuration")
        if not self.resource_id:
            raise ValueError("AZURE_SPEECH_RESOURCE_ID is required for Microsoft Entra ID authentication")

        # Microsoft Entra ID credential (Managed Identity in Azure, Azure CLI / VS Code sign-in locally)
        # Created lazily on first use so azure.identity isn't loaded at startup.
        self._cached_credential = None

    @property
    def _credential(self):
        """Lazily create and cache the Entra ID credential."""
        if self._cached_credential is None:
            self._cached_credential = config.create_credential()
        return self._cached_credential

    def _warm_up_credential(self) -> None:
        """Pre-acquire an Entra ID token before starting transcription.

        On a cold App Service instance the first ``get_token`` call walks the
        DefaultAzureCredential chain, which can be slow enough that the first
        ConversationTranscriber session stops before it is authenticated,
        producing a silent cancel with zero segments. Fetching (and caching) a
        token up front removes that race from the first real attempt.
        """
        try:
            self._credential.get_token(config.COGNITIVE_SCOPE)
            logger.debug("Entra ID credential warmed up for transcription")
        except Exception as ex:
            # Non-fatal: the transcription path will surface any real auth error.
            logger.debug(f"Credential warm-up skipped/failed (non-fatal): {ex}")

    def _create_speech_config(self) -> speechsdk.SpeechConfig:
        """Create Azure Speech SDK configuration"""
        import azure.cognitiveservices.speech as speechsdk

        logger.info(f"Creating speech config - Region: {self.region}")
        logger.info(
            f"Real-time Speech endpoint: {self.endpoint or 'None'} "
            f"(cloud: {config.AZURE_CLOUD})"
        )

        # Microsoft Entra ID authentication for SpeechRecognizer / ConversationTranscriber:
        # pass the TokenCredential directly together with the region-based real-time
        # WebSocket endpoint (wss://<region>.stt.speech.<cloud-suffix>). The SDK acquires
        # and refreshes the token internally. This is the officially supported pattern; the
        # manual 'aad#<resourceId>#<token>' authorization token does not work reliably with
        # endpoint-based config and results in a silent cancel with no segments.
        # Force the cloud-correct Cognitive Services token scope. The SDK otherwise
        # always requests the commercial audience, which the Azure US Government
        # Speech endpoint rejects (silent cancel, zero segments).
        scoped_credential = _ScopedTokenCredential(self._credential, config.COGNITIVE_SCOPE)
        speech_config = speechsdk.SpeechConfig(
            token_credential=scoped_credential,
            endpoint=self.endpoint
        )
        logger.info(
            f"Real-time SpeechConfig created with TokenCredential "
            f"(scope: {config.COGNITIVE_SCOPE}), endpoint: {self.endpoint}"
        )

        return speech_config

    def _run_transcription_session(self, audio_file_path: str, selected_locale: str):
        """Run a single ConversationTranscriber session over the audio file.

        Returns a tuple ``(segments, has_error, error_message,
        received_end_of_stream)``. Callers may retry when the session returns no
        error, no EndOfStream and zero segments, which indicates a silent
        cold-connection cancel rather than genuinely empty audio.
        """
        import azure.cognitiveservices.speech as speechsdk
        import os
        import tempfile

        speech_config = self._create_speech_config()
        speech_config.speech_recognition_language = selected_locale

        # Optionally enable the Speech SDK's native trace log. A failed real-time
        # session can stop cleanly with zero segments and NO canceled event (a
        # "silent cancel"), leaving the Python layer with no error to report. The
        # native SDK log captures the underlying WebSocket handshake / auth /
        # connection failure, which is the only way to diagnose that case.
        sdk_log_path = None
        if config.ENABLE_SPEECH_SDK_TRACE:
            try:
                sdk_log_path = os.path.join(
                    tempfile.gettempdir(), f"speechsdk_{int(time.time() * 1000)}.log"
                )
                speech_config.set_property(
                    speechsdk.PropertyId.Speech_LogFilename, sdk_log_path
                )
                logger.info(f"Speech SDK native trace enabled: {sdk_log_path}")
            except Exception as log_ex:
                sdk_log_path = None
                logger.debug(f"Could not enable Speech SDK trace (non-fatal): {log_ex}")

        # Create audio configuration
        audio_config = speechsdk.audio.AudioConfig(filename=audio_file_path)
        logger.debug(f"Audio config created for: {audio_file_path}")

        # Create conversation transcriber
        logger.debug("Creating ConversationTranscriber...")
        conversation_transcriber = speechsdk.transcription.ConversationTranscriber(
            speech_config=speech_config,
            audio_config=audio_config
        )
        logger.debug("ConversationTranscriber created")

        segments = []
        has_error = False
        error_message = None
        done = False
        # True only once the service has fully processed the audio (EndOfStream).
        # Without this, a connection failure can stop the session silently with zero
        # segments and be misreported as a successful "no speech detected" result.
        received_end_of_stream = False

        def transcribed_callback(evt: speechsdk.SessionEventArgs):
            """Handle transcribed events"""
            nonlocal segments

            if evt.result.reason == speechsdk.ResultReason.RecognizedSpeech:
                try:
                    speaker = evt.result.speaker_id if evt.result.speaker_id else "Unknown"
                    text = evt.result.text

                    logger.debug(f"Segment recognized: Speaker={speaker}, Text length={len(text)}")

                    segment = SpeakerSegment(
                        speaker=speaker,
                        original_speaker=speaker,
                        text=text,
                        original_text=text,
                        offset_in_ticks=evt.result.offset,
                        duration_in_ticks=0  # Will be calculated later
                    )

                    segments.append(segment)
                    logger.debug(f"Segment added. Total segments: {len(segments)}")

                except Exception as seg_ex:
                    logger.error(f"Error creating segment: {seg_ex}", exc_info=True)

            elif evt.result.reason == speechsdk.ResultReason.NoMatch:
                logger.debug("Speech could not be recognized (NoMatch)")

        def transcribing_callback(evt: speechsdk.SessionEventArgs):
            """Handle transcribing (intermediate) events"""
            logger.debug(f"TRANSCRIBING: {evt.result.text}")

        def canceled_callback(evt: speechsdk.SessionEventArgs):
            """Handle canceled events"""
            nonlocal has_error, error_message, done, received_end_of_stream

            cancellation_details = evt.cancellation_details

            logger.warning(
                f"Transcription canceled: {cancellation_details.reason} | "
                f"Code: {cancellation_details.error_code} | "
                f"Details: {cancellation_details.error_details}"
            )
            if cancellation_details.reason != speechsdk.CancellationReason.EndOfStream:
                logger.error(
                    f"Cancellation details - Code: {cancellation_details.error_code}, "
                    f"Details: {cancellation_details.error_details}"
                )
            else:
                logger.debug(f"Error code: {cancellation_details.error_code}, Details: {cancellation_details.error_details}")

            # EndOfStream is NOT an error - it means the audio file finished successfully
            if cancellation_details.reason == speechsdk.CancellationReason.EndOfStream:
                logger.info("Audio stream ended normally (EndOfStream). This is expected behavior.")
                # Don't set has_error - this is normal completion
                received_end_of_stream = True
                done = True
                return

            # Only treat as error if the reason is actually Error
            if cancellation_details.reason == speechsdk.CancellationReason.Error:
                has_error = True

                # Create specific error messages based on error code
                error_code = cancellation_details.error_code
                error_details = cancellation_details.error_details

                if error_code == speechsdk.CancellationErrorCode.AuthenticationFailure:
                    error_message = f"Authentication failed: Invalid subscription key or region. Details: {error_details}"
                elif error_code == speechsdk.CancellationErrorCode.BadRequest:
                    error_message = f"Bad request: The audio format may not be supported or the endpoint doesn't support ConversationTranscriber. Details: {error_details}"
                elif error_code == speechsdk.CancellationErrorCode.ConnectionFailure:
                    error_message = f"Connection failed: Unable to connect to Azure Speech Service. Details: {error_details}"
                elif error_code == speechsdk.CancellationErrorCode.ServiceTimeout:
                    error_message = f"Service timeout: The request took too long. Details: {error_details}"
                elif error_code == speechsdk.CancellationErrorCode.TooManyRequests:
                    error_message = f"Too many requests: Quota exceeded. Details: {error_details}"
                elif error_code == speechsdk.CancellationErrorCode.Forbidden:
                    error_message = f"Forbidden: Access denied. Check if ConversationTranscriber is enabled for your subscription. Details: {error_details}"
                elif error_code == speechsdk.CancellationErrorCode.ServiceUnavailable:
                    error_message = f"Service unavailable: Try again later. Details: {error_details}"
                else:
                    error_message = f"Error during transcription (Code: {error_code}): {error_details}"
            else:
                # Other cancellation reasons that aren't EndOfStream or Error
                error_message = f"Transcription canceled: {cancellation_details.reason}"
                has_error = True

            done = True

        def session_started_callback(evt: speechsdk.SessionEventArgs):
            """Handle session started event"""
            logger.info(f"Session started: {evt.session_id}")

        def session_stopped_callback(evt: speechsdk.SessionEventArgs):
            """Handle session stopped event"""
            nonlocal done
            logger.info(f"Session stopped: {evt.session_id}")
            done = True

        # Connect callbacks
        conversation_transcriber.transcribed.connect(transcribed_callback)
        conversation_transcriber.transcribing.connect(transcribing_callback)
        conversation_transcriber.canceled.connect(canceled_callback)
        conversation_transcriber.session_started.connect(session_started_callback)
        conversation_transcriber.session_stopped.connect(session_stopped_callback)

        # Attach connection-level diagnostics. A silent cancel often corresponds
        # to the WebSocket connecting and then immediately disconnecting; logging
        # these events makes that visible even when no canceled event fires.
        try:
            connection = speechsdk.Connection.from_recognizer(conversation_transcriber)
            connection.connected.connect(
                lambda evt: logger.info(f"Speech service connection established (session: {evt.session_id})")
            )
            connection.disconnected.connect(
                lambda evt: logger.warning(f"Speech service connection dropped (session: {evt.session_id})")
            )
        except Exception as conn_ex:
            logger.debug(f"Could not attach connection diagnostics (non-fatal): {conn_ex}")

        # Start transcription
        conversation_transcriber.start_transcribing_async().get()
        logger.info("Transcription started")

        # Wait for completion with configurable polling interval
        while not done:
            time.sleep(self.poll_interval)

        conversation_transcriber.stop_transcribing_async().get()
        logger.info(f"Transcription completed. Segments collected: {len(segments)}")

        # On a silent cancel (session stopped cleanly with no error, no
        # EndOfStream and zero segments) dump the tail of the SDK native log so
        # the real WebSocket/auth/connection failure is visible in app logs.
        silent_cancel = (
            not has_error and not received_end_of_stream and len(segments) == 0
        )
        if sdk_log_path:
            try:
                if silent_cancel and os.path.exists(sdk_log_path):
                    with open(sdk_log_path, 'r', errors='replace') as fh:
                        tail = fh.readlines()[-80:]
                    logger.warning(
                        "Speech SDK native trace (tail) for session that ended "
                        "with no response:\n" + "".join(tail)
                    )
            except Exception as read_ex:
                logger.debug(f"Could not read Speech SDK trace log (non-fatal): {read_ex}")
            finally:
                try:
                    if os.path.exists(sdk_log_path):
                        os.remove(sdk_log_path)
                except Exception:
                    pass

        return segments, has_error, error_message, received_end_of_stream

    def transcribe_with_diarization(
        self, 
        audio_file_path: str, 
        locale: Optional[str] = None
    ) -> TranscriptionResult:
        """
        Transcribe audio file with speaker diarization
        
        Args:
            audio_file_path: Path to the audio file
            locale: Language locale (IGNORED - Real-time transcription with 
                   ConversationTranscriber only supports en-US. This parameter 
                   is kept for API compatibility but will be ignored.)
            
        Returns:
            TranscriptionResult object containing segments and metadata
            
        Raises:
            TranscriptionException: If transcription fails
        """
        if not audio_file_path:
            raise ValueError("Audio file path cannot be empty")

        import os
        if not os.path.exists(audio_file_path):
            raise FileNotFoundError(f"Audio file not found: {audio_file_path}")
        
        # NOTE: Real-time transcription with ConversationTranscriber ONLY supports English (en-US)
        # This is a limitation of the Azure ConversationTranscriber API
        selected_locale = "en-US"
        
        # Warn user if they requested a different locale
        if locale and locale != "en-US":
            logger.warning(
                f"Real-time transcription only supports 'en-US'. "
                f"Requested locale '{locale}' will be ignored."
            )
        
        result = TranscriptionResult()

        try:
            # Log transcription start
            logger.info(f"Starting real-time transcription: {audio_file_path} (locale: {selected_locale})")
            logger.debug(f"Region: {self.region}, Endpoint: {self.endpoint or 'default'}")

            # Proactively acquire an Entra ID token so the first attempt isn't
            # racing a slow credential-chain probe on a cold instance.
            self._warm_up_credential()

            # A cold ConversationTranscriber connection occasionally stops the
            # session cleanly before any audio is processed: no error, no
            # EndOfStream and zero segments. Retrying the session transparently
            # recovers from this instead of surfacing a spurious failure.
            segments = []
            has_error = False
            error_message = None
            received_end_of_stream = False

            for attempt in range(1, self.max_transcription_attempts + 1):
                segments, has_error, error_message, received_end_of_stream = \
                    self._run_transcription_session(audio_file_path, selected_locale)

                silent_cancel = (
                    not has_error and not received_end_of_stream and len(segments) == 0
                )
                if not silent_cancel:
                    break

                if attempt < self.max_transcription_attempts:
                    logger.warning(
                        f"Real-time session ended with no response on attempt "
                        f"{attempt}/{self.max_transcription_attempts} "
                        f"(no error, no EndOfStream, 0 segments). Retrying..."
                    )
                    time.sleep(self.transcription_retry_delay_seconds)

            # Guard against a persistent silent failure to reach the Speech
            # service. If even after retries the session never processed the
            # audio, surface a clear error instead of a false "no speech
            # detected" result.
            if not has_error and not received_end_of_stream and len(segments) == 0:
                has_error = True
                error_message = (
                    "Could not reach the Azure Speech service. The transcription session "
                    "ended without processing the audio (no response received). Verify network "
                    "connectivity, the endpoint/region, and that the service is available."
                )
                logger.error(error_message)

            # Filter out segments where speaker is "Unknown" and text is empty
            filtered_segments = [
                s for s in segments
                if not (s.speaker.lower() == "unknown" and not s.text.strip())
            ]
            
            logger.info(f"Filtered segments: {len(filtered_segments)} of {len(segments)} kept")
            logger.debug(f"Removed {len(segments) - len(filtered_segments)} empty/unknown segments")
            
            # Calculate durations for segments
            logger.debug("Calculating segment durations...")
            for i in range(len(filtered_segments)):
                current_segment = filtered_segments[i]
                
                if i < len(filtered_segments) - 1:
                    # Duration = next segment's offset - current segment's offset
                    next_segment = filtered_segments[i + 1]
                    duration_ticks = next_segment.offset_in_ticks - current_segment.offset_in_ticks
                    current_segment.duration_in_ticks = max(duration_ticks, 0)
                else:
                    # Last segment: estimate duration based on text length using config values
                    word_count = len(current_segment.text.split())
                    estimated_seconds = max(
                        word_count / config.WORDS_PER_SECOND, 
                        config.MIN_SEGMENT_DURATION_SECONDS
                    )
                    current_segment.duration_in_ticks = int(estimated_seconds * 10_000_000)
                    logger.debug(f"Last segment duration estimated: {estimated_seconds:.2f}s ({word_count} words)")
            
            # Assign line numbers
            for i, segment in enumerate(filtered_segments, 1):
                segment.line_number = i
            
            if has_error:
                result.success = False
                result.message = error_message or "Unknown error occurred"
                raise AzureServiceException(result.message)
            else:
                result.success = True
                result.segments = filtered_segments
                
                # Build full transcript
                transcript_lines = [f"[{s.speaker}]: {s.text}" for s in filtered_segments]
                result.full_transcript = "\n".join(transcript_lines)
                
                # Calculate available speakers
                result.available_speakers = sorted(list(set(
                    s.speaker for s in filtered_segments if s.speaker.strip()
                )))
                
                # Calculate speaker statistics
                speaker_groups = {}
                for segment in filtered_segments:
                    if segment.speaker not in speaker_groups:
                        speaker_groups[segment.speaker] = []
                    speaker_groups[segment.speaker].append(segment)
                
                result.speaker_statistics = []
                for speaker, speaker_segments in speaker_groups.items():
                    total_time = sum(
                        s.end_time_in_seconds - s.start_time_in_seconds
                        for s in speaker_segments
                    )
                    first_appearance = min(s.start_time_in_seconds for s in speaker_segments)
                    
                    result.speaker_statistics.append(SpeakerInfo(
                        name=speaker,
                        segment_count=len(speaker_segments),
                        total_speak_time_seconds=total_time,
                        first_appearance_seconds=first_appearance
                    ))
                
                # Sort by first appearance
                result.speaker_statistics.sort(key=lambda x: x.first_appearance_seconds)
                
                if len(filtered_segments) == 0:
                    result.message = ("Transcription completed but no speech segments were detected. "
                                    "This could mean the audio has no speech, is too short, or "
                                    "diarization couldn't identify distinct speakers.")
                    logger.warning("No segments detected in transcription")
                else:
                    result.message = f"Transcription completed successfully with {len(filtered_segments)} segment(s)"
                    logger.info(f"Transcription successful: {len(filtered_segments)} segments")
            
            return result
            
        except (AzureServiceException, TranscriptionException):
            raise
        except Exception as ex:
            logger.error(f"Error during transcription: {ex}", exc_info=True)
            raise TranscriptionException(f"Transcription failed: {str(ex)}")


# Create a global instance
speech_to_text_service = SpeechToTextService()
