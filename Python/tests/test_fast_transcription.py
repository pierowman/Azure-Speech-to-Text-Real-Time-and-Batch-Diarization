"""Fast Transcription contract and upload-route tests; no Azure calls."""
import io
import json
import os
from pathlib import Path
import re
import sys
from unittest.mock import Mock, patch

import pytest
import requests
from azure.core.exceptions import ClientAuthenticationError
from werkzeug.datastructures import FileStorage

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
with patch.dict(os.environ, {
    'PYTHON_DOTENV_DISABLED': '1',
    'FLASK_SECRET_KEY': 'test-only-secret',
    'AZURE_SPEECH_REGION': 'eastus2',
    'AZURE_SUBSCRIPTION_ID': 'test-subscription',
    'AZURE_RESOURCE_GROUP': 'test-group',
    'AZURE_SPEECH_RESOURCE_NAME': 'test-speech',
    'AZURE_CLOUD': 'AzureCloud',
    'ENABLE_BLOB_STORAGE': 'false',
}):
    import app as app_module
    from config import config
    from exceptions import (
        AzureServiceException, ConfigurationException, InvalidAudioFileException,
        TranscriptionException, ValidationException,
    )
    from services.fast_service import FastTranscriptionService
    from validators import audio_file_validator


@pytest.fixture(autouse=True)
def settings(monkeypatch):
    monkeypatch.setattr(config, 'ENABLE_FAST_TRANSCRIPTION', True)
    monkeypatch.setattr(config, 'ENABLE_MAI_TRANSCRIPTION', True)
    monkeypatch.setattr(config, 'AZURE_CLOUD', 'AzureCloud')
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_RESOURCE_NAME', 'test-mai')
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_REGION', 'eastus')


@pytest.fixture
def payload():
    return {'phrases': [
        {'text': 'Hello.', 'speaker': 0, 'offsetMilliseconds': 1250,
         'durationMilliseconds': 500, 'locale': 'en-US'},
        {'text': 'Hi.', 'speaker': 1, 'offsetMilliseconds': 2000,
         'durationMilliseconds': 250, 'locale': 'en-US'},
    ], 'combinedPhrases': [{'text': 'Hello. Hi.'}], 'durationMilliseconds': 2250}


def response(status, body):
    result = requests.Response()
    result.status_code = status
    result._content = json.dumps(body).encode()
    return result


def test_standard_definition():
    definition = FastTranscriptionService().build_definition('azure-speech', 'en-US', True, 'verbatim')
    assert definition == {'locales': ['en-US'], 'diarization': {'enabled': True}}


def test_mai_definition():
    definition = FastTranscriptionService().build_definition('MAI-Transcribe-2', '', False, 'clean')
    assert 'locales' not in definition
    assert definition['diarization'] == {'enabled': False}
    assert definition['enhancedMode'] == {
        'enabled': True, 'model': 'MAI-Transcribe-2',
        'modelOptions': {'timestamps': 'segment', 'transcribeStyle': 'clean'},
    }


@pytest.mark.parametrize('model,locale,style', [
    ('invalid', '', 'verbatim'), ('azure-speech', '<script>', 'clean'),
    ('azure-speech', '', 'invalid'),
])
def test_invalid_options(model, locale, style):
    with pytest.raises(ValidationException):
        FastTranscriptionService().build_definition(model, locale, True, style)


def test_disabled_features(monkeypatch):
    service = FastTranscriptionService()
    monkeypatch.setattr(config, 'ENABLE_MAI_TRANSCRIPTION', False)
    with pytest.raises(ValidationException, match='disabled'):
        service.build_definition('MAI-Transcribe-2', '', True, 'clean')
    monkeypatch.setattr(config, 'ENABLE_FAST_TRANSCRIPTION', False)
    with pytest.raises(ConfigurationException, match='disabled'):
        service.build_definition('azure-speech', '', True, 'clean')


def test_unsupported_primary_region_has_actionable_error(monkeypatch):
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_RESOURCE_NAME', '')
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_REGION', '')
    service = FastTranscriptionService()
    with pytest.raises(ValidationException, match="'eastus2'"):
        service.build_definition('MAI-Transcribe-2', '', True, 'clean')
    assert 'AZURE_MAI_SPEECH_RESOURCE_NAME' in config.MAI_UNAVAILABLE_REASON
    assert service.build_definition('azure-speech', '', True, 'verbatim')


@pytest.mark.parametrize('region', [
    'centralindia', 'eastus', 'northeurope', 'southeastasia', 'westus', 'westus2',
])
def test_supported_mai_regions(monkeypatch, region):
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_REGION', region)
    assert config.MAI_UNAVAILABLE_REASON == ''


def test_supported_primary_resource_can_be_reused(monkeypatch):
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_RESOURCE_NAME', '')
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_REGION', '')
    monkeypatch.setattr(config, 'AZURE_SPEECH_REGION', 'eastus')
    assert config.MAI_UNAVAILABLE_REASON == ''
    assert config.MAI_SPEECH_ENDPOINT == config.AZURE_SPEECH_ENDPOINT
    assert config.MAI_SPEECH_REGION == 'eastus'


@pytest.mark.parametrize('name,region', [('test-mai', ''), ('', 'eastus')])
def test_partial_override_does_not_silently_fall_back(monkeypatch, name, region):
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_RESOURCE_NAME', name)
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_REGION', region)
    with pytest.raises(ValidationException, match='Set both'):
        FastTranscriptionService().build_definition('MAI-Transcribe-2', '', True, 'clean')


def test_mai_does_not_use_commercial_cloud_from_government(monkeypatch):
    monkeypatch.setattr(config, 'AZURE_CLOUD', 'AzureUSGovernment')
    assert config.MAI_SPEECH_ENDPOINT == 'https://test-mai.cognitiveservices.azure.us/'
    with pytest.raises(ValidationException, match='not documented as available'):
        FastTranscriptionService().build_definition('MAI-Transcribe-2', '', True, 'clean')


def test_parse_timing_speakers_statistics_and_raw_json(payload):
    payload['phrases'].reverse()
    result = FastTranscriptionService.parse_result(payload)
    assert result.success
    assert result.segments[0].speaker == 'Speaker 0'
    assert result.segments[0].start_time_in_seconds == 1.25
    assert result.segments[0].end_time_in_seconds == 1.75
    assert [segment.line_number for segment in result.segments] == [1, 2]
    assert result.available_speakers == ['Speaker 0', 'Speaker 1']
    assert result.speaker_statistics[0].total_speak_time_seconds == 0.5
    assert json.loads(result.raw_json_data) == payload
    assert result.full_transcript == '[Speaker 0]: Hello.\n[Speaker 1]: Hi.'


def test_without_diarization(payload):
    del payload['phrases'][0]['speaker']
    assert FastTranscriptionService.parse_result(payload).segments[0].speaker == 'Unknown'


@pytest.mark.parametrize('payload', [
    {}, [], {'phrases': [None]}, {'phrases': [{'text': 'Hello'}]},
    {'phrases': [{'text': 'Hello', 'offsetMilliseconds': -1, 'durationMilliseconds': 10}]},
])
def test_malformed_response(payload):
    with pytest.raises(AzureServiceException):
        FastTranscriptionService.parse_result(payload)


def test_no_speech():
    with pytest.raises(TranscriptionException, match='No speech'):
        FastTranscriptionService.parse_result({'phrases': []})


@pytest.mark.parametrize('cloud,suffix,scope', [
    ('AzureCloud', 'azure.com', 'https://cognitiveservices.azure.com/.default'),
    ('AzureUSGovernment', 'azure.us', 'https://cognitiveservices.azure.us/.default'),
])
def test_multipart_endpoint_and_auth(monkeypatch, tmp_path, payload, cloud, suffix, scope):
    monkeypatch.setattr(config, 'AZURE_CLOUD', cloud)
    audio = tmp_path / 'sample.wav'
    audio.write_bytes(b'audio-content')
    service = FastTranscriptionService()
    service._credential = Mock()
    service._credential.get_token.return_value.token = 'test-token'
    definition = service.build_definition('azure-speech', 'en-US', True, 'verbatim')

    def post(url, **kwargs):
        assert url == f'https://test-speech.cognitiveservices.{suffix}/speechtotext/transcriptions:transcribe'
        assert kwargs['params'] == {'api-version': config.FAST_TRANSCRIPTION_API_VERSION}
        assert kwargs['headers']['Authorization'] == 'Bearer test-token'
        assert 'Content-Type' not in kwargs['headers']
        assert kwargs['files']['audio'][1].read() == b'audio-content'
        assert json.loads(kwargs['data']['definition']) == definition
        assert kwargs['timeout'] == (15, config.FAST_TRANSCRIPTION_TIMEOUT_SECONDS)
        assert kwargs['allow_redirects'] is False
        return response(200, payload)

    monkeypatch.setattr(requests, 'post', post)
    assert service.transcribe(str(audio), definition).success
    service._credential.get_token.assert_called_once_with(scope)


@pytest.mark.parametrize('failure,expected', [
    (requests.Timeout(), 'timed out'),
    (requests.ConnectionError(), 'Could not reach'),
])
def test_network_errors(monkeypatch, tmp_path, failure, expected):
    audio = tmp_path / 'audio.wav'
    audio.write_bytes(b'audio')
    service = FastTranscriptionService()
    service._credential = Mock()
    service._credential.get_token.return_value.token = 'test-token'
    monkeypatch.setattr(requests, 'post', Mock(side_effect=failure))
    with pytest.raises(AzureServiceException, match=expected):
        service.transcribe(str(audio), {})


def test_mai_uses_separate_endpoint_and_preserves_primary(monkeypatch, tmp_path, payload):
    primary_endpoint = config.AZURE_SPEECH_ENDPOINT
    realtime_endpoint = config.REALTIME_SPEECH_ENDPOINT
    batch_endpoint = app_module.batch_transcription_service.base_url
    audio = tmp_path / 'audio.wav'
    audio.write_bytes(b'audio')
    service = FastTranscriptionService()
    service._credential = Mock()
    service._credential.get_token.return_value.token = 'test-token'
    post = Mock(return_value=response(200, payload))
    monkeypatch.setattr(requests, 'post', post)
    definition = service.build_definition('MAI-Transcribe-2', '', True, 'clean')
    assert service.transcribe(str(audio), definition).success
    assert post.call_args.args[0] == (
        'https://test-mai.cognitiveservices.azure.com/speechtotext/transcriptions:transcribe'
    )
    service._credential.get_token.assert_called_once_with(config.COGNITIVE_SCOPE)
    assert config.AZURE_SPEECH_ENDPOINT == primary_endpoint
    assert config.REALTIME_SPEECH_ENDPOINT == realtime_endpoint
    assert config.AZURE_SPEECH_REGION == 'eastus2'
    assert app_module.batch_transcription_service.base_url == batch_endpoint


def test_enhanced_mode_rejection_has_targeted_error_no_fallback(monkeypatch, tmp_path):
    audio = tmp_path / 'audio.wav'
    audio.write_bytes(b'audio')
    service = FastTranscriptionService()
    service._credential = Mock()
    service._credential.get_token.return_value.token = 'test-token'
    post = Mock(return_value=response(400, {
        'error': {'message': 'Enhanced mode with model is currently not supported yet.'},
    }))
    monkeypatch.setattr(requests, 'post', post)
    definition = service.build_definition('MAI-Transcribe-2', '', True, 'clean')
    with pytest.raises(AzureServiceException, match='configured region: eastus') as error:
        service.transcribe(str(audio), definition)
    assert 'actual region and model availability' in str(error.value)
    post.assert_called_once()


@pytest.mark.parametrize('status', [400, 401, 403, 404, 429, 500])
def test_upstream_errors(monkeypatch, tmp_path, status):
    audio = tmp_path / 'audio.wav'
    audio.write_bytes(b'audio')
    service = FastTranscriptionService()
    service._credential = Mock()
    service._credential.get_token.return_value.token = 'test-token'
    monkeypatch.setattr(requests, 'post', Mock(return_value=response(
        status, {'error': {'message': 'Model unavailable'}}
    )))
    with pytest.raises(AzureServiceException, match=f'HTTP {status}: Model unavailable'):
        service.transcribe(str(audio), {})


def test_authentication_failure(monkeypatch):
    service = FastTranscriptionService()
    service._credential = Mock()
    service._credential.get_token.side_effect = ClientAuthenticationError('test error')
    post = Mock()
    monkeypatch.setattr(requests, 'post', post)
    with pytest.raises(AzureServiceException, match='authenticate'):
        service.transcribe('unused.wav', {})
    post.assert_not_called()


@pytest.mark.parametrize('name,content', [('audio.exe', b'audio'), ('audio.wav', b'')])
def test_file_validation(name, content):
    with pytest.raises(InvalidAudioFileException):
        audio_file_validator.validate_file(FileStorage(io.BytesIO(content), filename=name), mode='fast')


def test_file_limit_and_stream_reset(monkeypatch):
    monkeypatch.setattr(config, 'FAST_MAX_FILE_SIZE', 3)
    audio = FileStorage(io.BytesIO(b'1234'), filename='audio.wav')
    with pytest.raises(InvalidAudioFileException, match='exceeds'):
        audio_file_validator.validate_file(audio, mode='fast')
    assert audio.tell() == 0


@pytest.fixture
def client(monkeypatch, tmp_path):
    monkeypatch.setattr(config, 'UPLOAD_FOLDER', str(tmp_path))
    monkeypatch.setitem(app_module.app.config, 'TESTING', True)
    monkeypatch.setitem(app_module.app.config, 'WTF_CSRF_ENABLED', False)
    monkeypatch.setitem(app_module.app.config, 'RATELIMIT_ENABLED', False)
    return app_module.app.test_client()


def test_upload_and_shared_result_contract(client, monkeypatch, payload, tmp_path):
    service = app_module.fast_transcription_service
    monkeypatch.setattr(service, 'transcribe', Mock(return_value=service.parse_result(payload)))
    result = client.post('/fast-transcribe', data={
        'audioFile': (io.BytesIO(b'RIFF-audio'), 'test.wav'),
        'model': 'MAI-Transcribe-2', 'enableDiarization': 'true', 'style': 'clean',
    })
    assert result.status_code == 200
    data = result.get_json()
    assert data['success'] and len(data['segments']) == 2
    assert data['audioFileUrl'].endswith('.wav')
    assert data['locale'] == 'en-US'
    assert data['model'] == 'MAI-Transcribe-2'
    assert json.loads(data['goldenRecordJsonData'])['segments'] == data['segments']
    assert json.loads(data['rawJsonData']) == payload
    assert len(list(tmp_path.iterdir())) == 1


def test_failed_upload_is_deleted(client, monkeypatch, tmp_path):
    monkeypatch.setattr(app_module.fast_transcription_service, 'transcribe',
                        Mock(side_effect=AzureServiceException('Test failure')))
    result = client.post('/fast-transcribe', data={'audioFile': (io.BytesIO(b'audio'), 'test.wav')})
    assert result.status_code == 502
    assert not list(tmp_path.iterdir())


@pytest.mark.parametrize('data', [
    {}, {'enableDiarization': 'invalid'}, {'model': 'unknown'},
])
def test_invalid_request_has_no_upload(client, data, tmp_path):
    assert client.post('/fast-transcribe', data=data).status_code == 400
    assert not list(tmp_path.iterdir())


def test_csrf_protected_upload(client, monkeypatch, payload):
    monkeypatch.setitem(app_module.app.config, 'WTF_CSRF_ENABLED', True)
    assert client.post('/fast-transcribe').status_code == 400
    page = client.get('/').get_data(as_text=True)
    token = re.search(r'name="csrf_token" value="([^"]+)"', page).group(1)
    monkeypatch.setattr(app_module.fast_transcription_service, 'transcribe',
                        Mock(return_value=FastTranscriptionService.parse_result(payload)))
    assert client.post('/fast-transcribe', data={
        'csrf_token': token, 'audioFile': (io.BytesIO(b'audio'), 'test.wav'),
    }).status_code == 200


def test_tab_and_disabled_model_rendering(client, monkeypatch):
    page = client.get('/').get_data(as_text=True)
    for name in ('realtime', 'fast', 'batch'):
        assert f'id="tab-{name}"' in page
        assert f'id="{name}-tab"' in page
    assert page.count('id="resultsSection"') == 1
    assert 'value="MAI-Transcribe-2"' in page
    monkeypatch.setattr(config, 'ENABLE_MAI_TRANSCRIPTION', False)
    assert 'value="MAI-Transcribe-2"' not in client.get('/').get_data(as_text=True)
    monkeypatch.setattr(config, 'ENABLE_FAST_TRANSCRIPTION', False)
    page = client.get('/').get_data(as_text=True)
    assert 'id="fastForm"' not in page
    assert 'Fast Transcription is disabled' in page
    assert client.post('/fast-transcribe').status_code == 500


def test_fast_tab_reuses_existing_theme(client):
    page = client.get('/').get_data(as_text=True)
    assert 'css/styles.css' in page
    assert 'css/fast-transcription.css' not in page
    assert 'scoutTheme' not in page
    assert 'data-theme' not in page
    assert 'class="info" id="fastStatus"' in page
    assert 'class="error" id="fastError"' in page


def test_unsupported_mai_hidden_and_rejected_before_upload(client, monkeypatch, tmp_path):
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_RESOURCE_NAME', '')
    monkeypatch.setattr(config, 'AZURE_MAI_SPEECH_REGION', '')
    page = client.get('/').get_data(as_text=True)
    assert 'value="MAI-Transcribe-2"' not in page
    assert 'not available in' in page
    assert 'eastus2' in page
    post = Mock()
    monkeypatch.setattr(requests, 'post', post)
    result = client.post('/fast-transcribe', data={
        'model': 'MAI-Transcribe-2', 'audioFile': (io.BytesIO(b'audio'), 'test.wav'),
    })
    assert result.status_code == 400
    assert 'AZURE_MAI_SPEECH_RESOURCE_NAME' in result.get_json()['message']
    assert not list(tmp_path.iterdir())
    post.assert_not_called()
