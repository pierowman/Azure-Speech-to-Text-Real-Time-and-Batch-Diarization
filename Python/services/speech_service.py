"""
Azure Speech-to-Text service with diarization support
"""
import logging
import json
import time
from typing import Optional
import azure.cognitiveservices.speech as speechsdk
from models import TranscriptionResult, SpeakerSegment, SpeakerInfo
from exceptions import TranscriptionException, AzureServiceException
from config import config

logger = logging.getLogger(__name__)


class SpeechToTextService:
    """Service for real-time speech transcription with diarization"""
    
    def __init__(self):
        self.subscription_key = config.AZURE_SPEECH_KEY
        self.region = config.AZURE_SPEECH_REGION
        self.endpoint = config.AZURE_SPEECH_ENDPOINT
        self.default_locale = config.DEFAULT_LOCALE
        self.poll_interval = config.TRANSCRIPTION_POLL_INTERVAL_SECONDS
        
        if not self.subscription_key:
            raise ValueError("Azure Speech subscription key not found in configuration")
        if not self.region:
            raise ValueError("Azure Speech region not found in configuration")
    
    def _create_speech_config(self) -> speechsdk.SpeechConfig:
        """Create Azure Speech SDK configuration"""
        # IMPORTANT: For ConversationTranscriber, use region-based configuration only
        # Custom endpoints may not support conversation transcription features
        logger.info(f"Creating speech config - Region: {self.region}")
        logger.info(f"Custom endpoint configured: {self.endpoint if self.endpoint else 'None'}")
        
        # Always use region-based config for ConversationTranscriber
        # Custom endpoints are primarily for speech-to-text, not conversation transcription
        speech_config = speechsdk.SpeechConfig(
            subscription=self.subscription_key,
            region=self.region
        )
        
        logger.info(f"Using region-based endpoint: {self.region}.stt.speech.microsoft.com")
        
        return speech_config
    
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
        segments = []
        
        try:
            # Log transcription start
            logger.info(f"Starting real-time transcription: {audio_file_path} (locale: {selected_locale})")
            logger.debug(f"Region: {self.region}, Endpoint: {self.endpoint or 'default'}")
            
            speech_config = self._create_speech_config()
            speech_config.speech_recognition_language = selected_locale
            
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
            
            has_error = False
            error_message = None
            done = False
            
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
                nonlocal has_error, error_message, done
                
                cancellation_details = evt.cancellation_details
                
                logger.warning(f"Transcription canceled: {cancellation_details.reason}")
                logger.debug(f"Error code: {cancellation_details.error_code}, Details: {cancellation_details.error_details}")
                
                # EndOfStream is NOT an error - it means the audio file finished successfully
                if cancellation_details.reason == speechsdk.CancellationReason.EndOfStream:
                    logger.info("Audio stream ended normally (EndOfStream). This is expected behavior.")
                    # Don't set has_error - this is normal completion
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
                logger.debug(f"Session started: {evt.session_id}")
            
            def session_stopped_callback(evt: speechsdk.SessionEventArgs):
                """Handle session stopped event"""
                nonlocal done
                logger.debug(f"Session stopped: {evt.session_id}")
                done = True
            
            # Connect callbacks
            conversation_transcriber.transcribed.connect(transcribed_callback)
            conversation_transcriber.transcribing.connect(transcribing_callback)
            conversation_transcriber.canceled.connect(canceled_callback)
            conversation_transcriber.session_started.connect(session_started_callback)
            conversation_transcriber.session_stopped.connect(session_stopped_callback)
            
            # Start transcription
            conversation_transcriber.start_transcribing_async().get()
            logger.info("Transcription started")
            
            # Wait for completion with configurable polling interval
            while not done:
                time.sleep(self.poll_interval)
            
            conversation_transcriber.stop_transcribing_async().get()
            logger.info(f"Transcription completed. Segments collected: {len(segments)}")
            
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
