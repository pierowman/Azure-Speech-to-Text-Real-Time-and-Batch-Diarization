"""
Configuration settings for the Speech-to-Text application
"""
from __future__ import annotations

import os
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


class Config:
    """Base application configuration"""
    
    # Flask settings
    SECRET_KEY = os.getenv('FLASK_SECRET_KEY')
    DEBUG = os.getenv('FLASK_DEBUG', 'false').lower() == 'true'
    
    # Azure Speech Service - REQUIRED (authenticated via Microsoft Entra ID / Managed Identity)
    AZURE_SPEECH_REGION = os.getenv('AZURE_SPEECH_REGION')
    # Components used to build the Speech resource's ARM resource ID (required for
    # real-time SDK Entra ID auth). The full ID is exposed via AZURE_SPEECH_RESOURCE_ID.
    AZURE_SUBSCRIPTION_ID = os.getenv('AZURE_SUBSCRIPTION_ID')
    AZURE_RESOURCE_GROUP = os.getenv('AZURE_RESOURCE_GROUP')
    AZURE_SPEECH_RESOURCE_NAME = os.getenv('AZURE_SPEECH_RESOURCE_NAME')

    # Azure Cloud - 'AzureCloud' (commercial) or 'AzureUSGovernment'
    AZURE_CLOUD = os.getenv('AZURE_CLOUD', 'AzureCloud')
    FEATURES_UNAVAILABLE_IN_AZURE_GOVERNMENT = (
        'Custom voice',
        'Personal voice',
        'Text to speech avatar',
        'Fast transcription',
        'Pronunciation assessment',
        'Custom keyword',
        'Voice Live',
        'Live interpreter',
        'Video translation',
        'LLM speech',
    )

    # Cloud-specific endpoint suffixes, token audiences, and authorities
    _CLOUD_CONFIG = {
        'AzureCloud': {
            'storage_suffix': 'blob.core.windows.net',
            'storage_audience': 'https://storage.azure.com',
            'authority_host': 'https://login.microsoftonline.com',
            'cognitive_suffix': 'api.cognitive.microsoft.com',
            'cognitive_audience': 'https://cognitiveservices.azure.com',
            'cognitive_endpoint_suffix': 'cognitiveservices.azure.com',
            'speech_host_suffix': 'stt.speech.microsoft.com',
        },
        'AzureUSGovernment': {
            'storage_suffix': 'blob.core.usgovcloudapi.net',
            'storage_audience': 'https://storage.azure.us',
            'authority_host': 'https://login.microsoftonline.us',
            'cognitive_suffix': 'api.cognitive.microsoft.us',
            'cognitive_audience': 'https://cognitiveservices.azure.us',
            'cognitive_endpoint_suffix': 'cognitiveservices.azure.us',
            'speech_host_suffix': 'stt.speech.azure.us',
        },
    }
    
    # Azure Storage - OPTIONAL (for batch transcription)
    AZURE_STORAGE_ACCOUNT_NAME = os.getenv('AZURE_STORAGE_ACCOUNT_NAME')
    AZURE_STORAGE_CONTAINER_NAME = os.getenv('AZURE_STORAGE_CONTAINER_NAME', 'speech-transcriptions')
    ENABLE_BLOB_STORAGE = os.getenv('ENABLE_BLOB_STORAGE', 'false').lower() == 'true'
    # Optional: client ID of a user-assigned managed identity. Leave blank to use a
    # system-assigned managed identity in Azure, or local sign-in (Azure CLI / VS Code) for dev.
    AZURE_CLIENT_ID = os.getenv('AZURE_CLIENT_ID')
    
    # Upload settings
    UPLOAD_FOLDER = os.getenv('UPLOAD_FOLDER', 'static/uploads')
    MAX_CONTENT_LENGTH = int(os.getenv('MAX_CONTENT_LENGTH', 524288000))  # 500MB
    
    # Audio file extensions
    REALTIME_ALLOWED_EXTENSIONS = os.getenv('REALTIME_ALLOWED_EXTENSIONS', '.wav').split(',')
    BATCH_ALLOWED_EXTENSIONS = os.getenv('BATCH_ALLOWED_EXTENSIONS', '.wav,.mp3,.ogg,.flac,.opus,.m4a,.webm').split(',')
    
    # Default settings
    DEFAULT_LOCALE = os.getenv('DEFAULT_LOCALE', 'en-US')
    DEFAULT_MIN_SPEAKERS = int(os.getenv('DEFAULT_MIN_SPEAKERS', 2))
    DEFAULT_MAX_SPEAKERS = int(os.getenv('DEFAULT_MAX_SPEAKERS', 5))
    
    # Batch job settings
    SHOW_TRANSCRIPTION_JOBS_TAB = os.getenv('SHOW_TRANSCRIPTION_JOBS_TAB', 'true').lower() == 'true'
    ENABLE_BATCH_TRANSCRIPTION = os.getenv('ENABLE_BATCH_TRANSCRIPTION', 'true').lower() == 'true'
    BATCH_JOB_AUTO_REFRESH_SECONDS = int(os.getenv('BATCH_JOB_AUTO_REFRESH_SECONDS', 60))

    # Sovereign-cloud availability must be confirmed before explicitly enabling.
    ENABLE_FAST_TRANSCRIPTION = (
        os.getenv('ENABLE_FAST_TRANSCRIPTION', str(AZURE_CLOUD == 'AzureCloud')).lower() == 'true'
        and AZURE_CLOUD == 'AzureCloud'
    )
    ENABLE_MAI_TRANSCRIPTION = (
        os.getenv('ENABLE_MAI_TRANSCRIPTION', str(AZURE_CLOUD == 'AzureCloud')).lower() == 'true'
        and AZURE_CLOUD == 'AzureCloud'
    )
    AZURE_MAI_SPEECH_RESOURCE_NAME = os.getenv('AZURE_MAI_SPEECH_RESOURCE_NAME', '').strip()
    AZURE_MAI_SPEECH_REGION = os.getenv('AZURE_MAI_SPEECH_REGION', '').strip()
    # MAI via transcriptions:transcribe (not Voice Live), documented October 2026.
    MAI_SUPPORTED_REGIONS = (
        'centralindia', 'eastus', 'northeurope', 'southeastasia', 'westus', 'westus2',
    )
    FAST_TRANSCRIPTION_API_VERSION = os.getenv('FAST_TRANSCRIPTION_API_VERSION', '2025-10-15')
    FAST_TRANSCRIPTION_TIMEOUT_SECONDS = int(os.getenv('FAST_TRANSCRIPTION_TIMEOUT_SECONDS', 180))
    FAST_ALLOWED_EXTENSIONS = ['.wav', '.mp3', '.flac']
    FAST_MAX_FILE_SIZE = 250 * 1024 * 1024
    
    # Cache settings
    LOCALES_CACHE_DURATION_HOURS = int(os.getenv('LOCALES_CACHE_DURATION_HOURS', 24))
    
    # Audio playback settings
    KEEP_AUDIO_FILES = os.getenv('KEEP_AUDIO_FILES', 'true').lower() == 'true'
    AUDIO_FILE_RETENTION_HOURS = int(os.getenv('AUDIO_FILE_RETENTION_HOURS', 24))
    
    # Transcription estimation constants
    WORDS_PER_SECOND = float(os.getenv('WORDS_PER_SECOND', 2.5))  # Average speaking rate
    MIN_SEGMENT_DURATION_SECONDS = float(os.getenv('MIN_SEGMENT_DURATION_SECONDS', 2.0))
    
    # File size limits (in bytes)
    REALTIME_MAX_FILE_SIZE = int(os.getenv('REALTIME_MAX_FILE_SIZE', 100 * 1024 * 1024))  # 100 MB
    BATCH_MAX_FILE_SIZE = int(os.getenv('BATCH_MAX_FILE_SIZE', 1024 * 1024 * 1024))  # 1 GB
    BATCH_MAX_FILES = int(os.getenv('BATCH_MAX_FILES', 100))
    
    # Rate limiting
    RATE_LIMIT_PER_MINUTE = int(os.getenv('RATE_LIMIT_PER_MINUTE', 10))
    
    # Polling configuration
    TRANSCRIPTION_POLL_INTERVAL_SECONDS = float(os.getenv('TRANSCRIPTION_POLL_INTERVAL_SECONDS', 0.5))

    # Real-time transcription retry configuration. A cold ConversationTranscriber
    # connection occasionally stops the session cleanly before any audio is
    # processed (no error, no EndOfStream, zero segments). Retrying the session
    # transparently recovers from this instead of surfacing a spurious failure.
    TRANSCRIPTION_MAX_ATTEMPTS = int(os.getenv('TRANSCRIPTION_MAX_ATTEMPTS', 3))
    TRANSCRIPTION_RETRY_DELAY_SECONDS = float(os.getenv('TRANSCRIPTION_RETRY_DELAY_SECONDS', 1.0))

    # When true, the real-time ConversationTranscriber enables the Speech SDK's
    # native trace log and, on a session that ends with no response (silent
    # cancel), writes the tail of that trace to the application log. This exposes
    # the underlying WebSocket / authentication / connection failure that the
    # Python SDK surface otherwise hides. Safe to leave on; turn off to reduce
    # log volume once real-time is confirmed healthy in all clouds.
    ENABLE_SPEECH_SDK_TRACE = os.getenv('ENABLE_SPEECH_SDK_TRACE', 'true').lower() == 'true'
    
    @property
    def _cloud(self):
        """Get the endpoint configuration for the selected Azure cloud"""
        return self._CLOUD_CONFIG.get(self.AZURE_CLOUD, self._CLOUD_CONFIG['AzureCloud'])

    @property
    def IS_AZURE_GOVERNMENT(self):
        """Return true when running in Azure Government."""
        return self.AZURE_CLOUD == 'AzureUSGovernment'

    @property
    def BLOB_SERVICE_ENDPOINT(self):
        """Get the blob service endpoint URL"""
        if self.AZURE_STORAGE_ACCOUNT_NAME:
            return f"https://{self.AZURE_STORAGE_ACCOUNT_NAME}.{self._cloud['storage_suffix']}"
        return ""

    @property
    def STORAGE_AUDIENCE(self):
        """Get the storage OAuth token audience for the selected cloud"""
        return self._cloud['storage_audience']

    @property
    def COGNITIVE_AUDIENCE(self):
        """Get the Cognitive Services OAuth token audience for the selected cloud"""
        return self._cloud['cognitive_audience']

    @property
    def COGNITIVE_SCOPE(self):
        """Get the Cognitive Services (Speech) Entra ID token scope for the selected cloud"""
        return f"{self.COGNITIVE_AUDIENCE}/.default"

    @property
    def AZURE_SPEECH_RESOURCE_ID(self):
        """Build the Speech resource's full ARM resource ID from its components"""
        if (self.AZURE_SUBSCRIPTION_ID and
                self.AZURE_RESOURCE_GROUP and
                self.AZURE_SPEECH_RESOURCE_NAME):
            return (
                f"/subscriptions/{self.AZURE_SUBSCRIPTION_ID}"
                f"/resourceGroups/{self.AZURE_RESOURCE_GROUP}"
                f"/providers/Microsoft.CognitiveServices/accounts/{self.AZURE_SPEECH_RESOURCE_NAME}"
            )
        return ""

    @property
    def AZURE_SPEECH_ENDPOINT(self):
        """Build the Speech resource endpoint URL from the resource name and cloud suffix"""
        if self.AZURE_SPEECH_RESOURCE_NAME:
            return f"https://{self.AZURE_SPEECH_RESOURCE_NAME}.{self._cloud['cognitive_endpoint_suffix']}/"
        return ""

    @property
    def MAI_SPEECH_REGION(self):
        """Use the optional MAI resource's region without changing other workflows."""
        return (self.AZURE_MAI_SPEECH_REGION or self.AZURE_SPEECH_REGION or '').strip().lower()

    @property
    def MAI_SPEECH_ENDPOINT(self):
        """Build the MAI resource endpoint in the same configured Azure cloud."""
        if self.AZURE_MAI_SPEECH_RESOURCE_NAME:
            return f"https://{self.AZURE_MAI_SPEECH_RESOURCE_NAME}.{self._cloud['cognitive_endpoint_suffix']}/"
        return self.AZURE_SPEECH_ENDPOINT

    @property
    def MAI_UNAVAILABLE_REASON(self):
        """Share MAI configuration/availability validation between the UI and API."""
        if not self.ENABLE_MAI_TRANSCRIPTION:
            return "MAI-Transcribe-2 is disabled for this environment."
        if bool(self.AZURE_MAI_SPEECH_RESOURCE_NAME) != bool(self.AZURE_MAI_SPEECH_REGION):
            return (
                "Set both AZURE_MAI_SPEECH_RESOURCE_NAME and AZURE_MAI_SPEECH_REGION "
                "to use a separate MAI resource."
            )
        if self.AZURE_CLOUD != 'AzureCloud':
            return "MAI-Transcribe-2 through Fast Transcription is not documented as available in this Azure cloud."
        if self.MAI_SPEECH_REGION not in self.MAI_SUPPORTED_REGIONS:
            return (
                f"MAI-Transcribe-2 through Fast Transcription is not available in "
                f"'{self.MAI_SPEECH_REGION}'. Configure AZURE_MAI_SPEECH_RESOURCE_NAME "
                f"and AZURE_MAI_SPEECH_REGION for a resource in a supported region: "
                f"{', '.join(self.MAI_SUPPORTED_REGIONS)}."
            )
        return ""

    @property
    def REALTIME_SPEECH_ENDPOINT(self):
        """Endpoint used by the real-time Speech SDK (ConversationTranscriber).

        Uses the resource's custom-domain endpoint
        (https://<resource-name>.cognitiveservices.azure.<suffix>/), the same
        cloud-aware endpoint used for batch. Microsoft Entra ID (token credential)
        authentication REQUIRES this custom-subdomain endpoint; a region-based
        WebSocket host (wss://<region>.stt.speech.<suffix>) cannot authenticate
        with a bearer token and causes the session to cancel silently with zero
        segments.
        """
        return self.AZURE_SPEECH_ENDPOINT

    @property
    def AUTHORITY_HOST(self):
        """Get the Entra ID (AAD) authority host for the selected cloud"""
        return self._cloud['authority_host']

    @property
    def COGNITIVE_SUFFIX(self):
        """Get the Cognitive Services endpoint suffix for the selected cloud"""
        return self._cloud['cognitive_suffix']

    @property
    def IS_CONFIGURED(self):
        """Check if Azure Storage is properly configured"""
        return (self.ENABLE_BLOB_STORAGE and 
                bool(self.AZURE_STORAGE_ACCOUNT_NAME) and 
                bool(self.AZURE_STORAGE_CONTAINER_NAME))

    def get_model_options(self, mode: str):
        """Return a list of selectable models for each transcription mode.

        This is deliberately server-side and region-aware rather than a generic live
        Azure discovery API. The Speech service exposes different capability sets by
        cloud, region, and model family, so the safest pattern is an explicit allowlist
        that is validated before every request.
        """
        normalized_mode = (mode or 'realtime').lower()
        model_catalog = {
            'realtime': [
                {'value': 'azure-speech', 'label': 'Azure Speech (default)', 'default': True},
            ],
            'batch': [
                {'value': 'azure-speech', 'label': 'Azure Speech (default)', 'default': True},
            ],
            'fast': [
                {'value': 'azure-speech', 'label': 'Azure Speech (default)', 'default': True},
                {'value': 'MAI-Transcribe-2', 'label': 'MAI-Transcribe-2', 'default': False},
            ],
        }
        options = model_catalog.get(normalized_mode, model_catalog['realtime'])
        if normalized_mode == 'fast':
            if not self.ENABLE_MAI_TRANSCRIPTION:
                return [option for option in options if option['value'] != 'MAI-Transcribe-2']
            if self.MAI_UNAVAILABLE_REASON:
                return [option for option in options if option['value'] != 'MAI-Transcribe-2']
        return options

    def validate_model_selection(self, mode: str, model: str) -> str:
        """Validate that a requested model is supported for the selected mode."""
        from exceptions import ValidationException

        normalized_mode = (mode or 'realtime').lower()
        selected_model = (model or 'azure-speech').strip()
        available = {option['value'] for option in self.get_model_options(normalized_mode)}
        if selected_model not in available:
            raise ValidationException(
                f"The selected model '{selected_model}' is not supported for {normalized_mode} transcription. "
                f"Choose from: {', '.join(sorted(available))}."
            )
        if selected_model == 'MAI-Transcribe-2' and self.MAI_UNAVAILABLE_REASON:
            raise ValidationException(self.MAI_UNAVAILABLE_REASON)
        return selected_model

    def create_credential(self) -> DefaultAzureCredential:
        """Create a DefaultAzureCredential for Microsoft Entra ID authentication.

        Works with a Managed Identity in Azure and with Azure CLI / VS Code
        sign-in for local development. Set AZURE_CLIENT_ID for a user-assigned
        managed identity; leave it blank for system-assigned or local sign-in.
        """
        # Imported lazily so the (heavy) azure.identity package is only loaded
        # when a credential is actually needed, not at application startup.
        from azure.identity import DefaultAzureCredential

        credential_kwargs = {'authority': self.AUTHORITY_HOST}
        if self.AZURE_CLIENT_ID:
            credential_kwargs['managed_identity_client_id'] = self.AZURE_CLIENT_ID
        return DefaultAzureCredential(**credential_kwargs)
    
    def validate(self):
        """Validate required configuration values"""
        errors = []
        
        # Critical settings
        if not self.SECRET_KEY:
            errors.append("FLASK_SECRET_KEY is required")
        
        if not self.AZURE_SPEECH_REGION:
            errors.append("AZURE_SPEECH_REGION is required")

        if not self.AZURE_SPEECH_RESOURCE_ID:
            errors.append(
                "AZURE_SUBSCRIPTION_ID, AZURE_RESOURCE_GROUP and AZURE_SPEECH_RESOURCE_NAME "
                "are all required to build the Speech resource ID for Microsoft Entra ID authentication"
            )

        # Blob storage validation (if enabled)
        if self.ENABLE_BLOB_STORAGE:
            if not self.AZURE_STORAGE_ACCOUNT_NAME:
                errors.append("AZURE_STORAGE_ACCOUNT_NAME is required when ENABLE_BLOB_STORAGE=true")
        
        if errors:
            raise ValueError(f"Configuration validation failed:\n" + "\n".join(f"  - {err}" for err in errors))


class DevelopmentConfig(Config):
    """Development environment configuration"""
    DEBUG = True


class ProductionConfig(Config):
    """Production environment configuration"""
    DEBUG = False
    
    def validate(self):
        """Additional production validation"""
        super().validate()
        
        if self.SECRET_KEY == 'dev-secret-key-change-in-production':
            raise ValueError("You must set a secure SECRET_KEY for production!")


# Environment-specific config selection
def get_config():
    """Get configuration based on environment"""
    env = os.getenv('FLASK_ENV', 'development').lower()
    
    if env == 'production':
        return ProductionConfig()
    else:
        return DevelopmentConfig()


# Create config instance
config = get_config()

# Validate configuration on import
try:
    config.validate()
except ValueError as e:
    import sys
    print(f"\n{'='*60}")
    print("CONFIGURATION ERROR")
    print('='*60)
    print(str(e))
    print(f"{'='*60}\n")
    print("Please check your .env file or environment variables.")
    print("See .env.example for reference.\n")
    sys.exit(1)
