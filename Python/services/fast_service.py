"""Synchronous Azure Speech Fast Transcription with optional MAI enhancement."""
import json
import logging
import os
import re

import requests
from azure.core.exceptions import ClientAuthenticationError

from config import config
from exceptions import (
    AzureServiceException, ConfigurationException, TranscriptionException,
    ValidationException,
)
from models import SpeakerSegment, TranscriptionResult
from route_helpers import assign_line_numbers, calculate_speaker_statistics, build_full_transcript

logger = logging.getLogger(__name__)


class FastTranscriptionService:
    """Upload audio directly to Speech; no Blob Storage or batch job required."""

    def __init__(self):
        self._credential = None

    def build_definition(self, model: str, locale: str, diarization: bool, style: str) -> dict:
        if not config.ENABLE_FAST_TRANSCRIPTION:
            raise ConfigurationException("Fast Transcription is disabled for this environment.")
        if model not in ('azure-speech', 'MAI-Transcribe-2'):
            raise ValidationException("Select Azure Speech or MAI-Transcribe-2.")
        if model == 'MAI-Transcribe-2' and config.MAI_UNAVAILABLE_REASON:
            raise ValidationException(config.MAI_UNAVAILABLE_REASON)
        if locale and not re.fullmatch(r'[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*', locale):
            raise ValidationException("Enter a language code such as en-US, or leave it blank.")
        if style not in ('verbatim', 'clean'):
            raise ValidationException("Transcript style must be verbatim or clean.")
        definition = {'diarization': {'enabled': diarization}}
        if locale:
            definition['locales'] = [locale]
        if model == 'MAI-Transcribe-2':
            definition['enhancedMode'] = {
                'enabled': True,
                'model': model,
                'modelOptions': {'timestamps': 'segment', 'transcribeStyle': style},
            }
        return definition

    def transcribe(self, file_path: str, definition: dict) -> TranscriptionResult:
        model = definition.get('enhancedMode', {}).get('model', 'azure-speech')
        if model == 'MAI-Transcribe-2':
            if config.MAI_UNAVAILABLE_REASON:
                raise ValidationException(config.MAI_UNAVAILABLE_REASON)
            endpoint = config.MAI_SPEECH_ENDPOINT.rstrip('/')
            region = config.MAI_SPEECH_REGION
        else:
            endpoint = config.AZURE_SPEECH_ENDPOINT.rstrip('/')
            region = config.AZURE_SPEECH_REGION
        if not endpoint:
            raise ConfigurationException("A Speech resource endpoint is required.")
        url = f"{endpoint}/speechtotext/transcriptions:transcribe"
        logger.info(
            "Fast Transcription endpoint: %s (cloud: %s, region: %s, model: %s, api-version: %s)",
            url, config.AZURE_CLOUD, region, model, config.FAST_TRANSCRIPTION_API_VERSION,
        )
        if self._credential is None:
            self._credential = config.create_credential()
        try:
            token = self._credential.get_token(config.COGNITIVE_SCOPE)
        except ClientAuthenticationError as ex:
            raise AzureServiceException(
                "Could not authenticate to Speech using Microsoft Entra ID."
            ) from ex
        try:
            with open(file_path, 'rb') as audio:
                response = requests.post(
                    url,
                    params={'api-version': config.FAST_TRANSCRIPTION_API_VERSION},
                    headers={'Authorization': f'Bearer {token.token}', 'Accept': 'application/json'},
                    files={'audio': (os.path.basename(file_path), audio)},
                    data={'definition': json.dumps(definition)},
                    timeout=(15, config.FAST_TRANSCRIPTION_TIMEOUT_SECONDS),
                    allow_redirects=False,
                )
        except requests.Timeout as ex:
            raise AzureServiceException(
                "Fast Transcription timed out. Try a shorter recording or use Batch Transcription."
            ) from ex
        except requests.RequestException as ex:
            raise AzureServiceException(
                "Could not reach the Fast Transcription endpoint. Check network access and region support."
            ) from ex
        if response.status_code != 200:
            try:
                body = response.json()
            except requests.exceptions.JSONDecodeError:
                body = {}
            error = body.get('error', body) if isinstance(body, dict) else {}
            message = (
                error.get('message', 'No error details returned.')
                if isinstance(error, dict) else 'No error details returned.'
            )
            logger.error("Fast Transcription HTTP %s: %s", response.status_code, message)
            if (model == 'MAI-Transcribe-2' and response.status_code == 400
                    and 'enhanced mode with model' in str(message).lower()
                    and 'not supported' in str(message).lower()):
                raise AzureServiceException(
                    f"The Speech endpoint {endpoint} (configured region: {region}) "
                    f"rejected MAI-Transcribe-2: {message} "
                    "Confirm the MAI resource's actual region and model availability. "
                    "Changing a region setting alone does not move an Azure resource."
                )
            raise AzureServiceException(
                f"Fast Transcription returned HTTP {response.status_code}: {message} "
                "Verify model/region availability and Speech permissions."
            )
        try:
            payload = response.json()
        except requests.exceptions.JSONDecodeError as ex:
            raise AzureServiceException("Speech returned an invalid JSON response.") from ex
        return self.parse_result(payload)

    @staticmethod
    def parse_result(payload: dict) -> TranscriptionResult:
        if not isinstance(payload, dict) or not isinstance(payload.get('phrases'), list):
            raise AzureServiceException("Speech returned a response without phrase results.")
        segments = []
        for phrase in payload['phrases']:
            if not isinstance(phrase, dict) or not isinstance(phrase.get('text'), str):
                raise AzureServiceException("Speech returned an invalid phrase.")
            if not phrase['text'].strip():
                continue
            offset = phrase.get('offsetMilliseconds')
            duration = phrase.get('durationMilliseconds')
            if (not isinstance(offset, (int, float)) or not isinstance(duration, (int, float))
                    or offset < 0 or duration < 0):
                raise AzureServiceException("Speech returned a phrase without valid timestamps.")
            speaker_id = phrase.get('speaker')
            segments.append(SpeakerSegment(
                speaker=f"Speaker {speaker_id}" if speaker_id is not None else "Unknown",
                text=phrase['text'],
                offset_in_ticks=round(offset * 10_000),
                duration_in_ticks=round(duration * 10_000),
            ))
        if not segments:
            raise TranscriptionException("No speech was recognized in the recording.")
        segments.sort(key=lambda segment: segment.offset_in_ticks)
        assign_line_numbers(segments)
        speakers, statistics = calculate_speaker_statistics(segments)
        logger.info("Fast Transcription successful: %s segments", len(segments))
        return TranscriptionResult(
            success=True,
            message=f"Fast transcription completed: {len(segments)} segments.",
            segments=segments,
            full_transcript=build_full_transcript(segments),
            available_speakers=speakers,
            speaker_statistics=statistics,
            raw_json_data=json.dumps(payload, indent=2),
        )


fast_transcription_service = FastTranscriptionService()
