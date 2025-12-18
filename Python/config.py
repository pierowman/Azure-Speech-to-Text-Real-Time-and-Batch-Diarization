"""
Configuration settings for the Speech-to-Text application
"""
import os
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


class Config:
    """Base application configuration"""
    
    # Flask settings
    SECRET_KEY = os.getenv('FLASK_SECRET_KEY')
    DEBUG = os.getenv('FLASK_DEBUG', 'false').lower() == 'true'
    
    # Azure Speech Service - REQUIRED
    AZURE_SPEECH_KEY = os.getenv('AZURE_SPEECH_KEY')
    AZURE_SPEECH_REGION = os.getenv('AZURE_SPEECH_REGION')
    AZURE_SPEECH_ENDPOINT = os.getenv('AZURE_SPEECH_ENDPOINT')
    
    # Azure Storage - OPTIONAL (for batch transcription)
    AZURE_STORAGE_ACCOUNT_NAME = os.getenv('AZURE_STORAGE_ACCOUNT_NAME')
    AZURE_STORAGE_CONTAINER_NAME = os.getenv('AZURE_STORAGE_CONTAINER_NAME', 'speech-transcriptions')
    ENABLE_BLOB_STORAGE = os.getenv('ENABLE_BLOB_STORAGE', 'false').lower() == 'true'
    USE_MANAGED_IDENTITY = os.getenv('USE_MANAGED_IDENTITY', 'false').lower() == 'true'
    AZURE_TENANT_ID = os.getenv('AZURE_TENANT_ID')
    AZURE_CLIENT_ID = os.getenv('AZURE_CLIENT_ID')
    AZURE_CLIENT_SECRET = os.getenv('AZURE_CLIENT_SECRET')
    
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
    
    @property
    def BLOB_SERVICE_ENDPOINT(self):
        """Get the blob service endpoint URL"""
        if self.AZURE_STORAGE_ACCOUNT_NAME:
            return f"https://{self.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net"
        return ""
    
    @property
    def IS_CONFIGURED(self):
        """Check if Azure Storage is properly configured"""
        return (self.ENABLE_BLOB_STORAGE and 
                bool(self.AZURE_STORAGE_ACCOUNT_NAME) and 
                bool(self.AZURE_STORAGE_CONTAINER_NAME))
    
    def validate(self):
        """Validate required configuration values"""
        errors = []
        
        # Critical settings
        if not self.SECRET_KEY:
            errors.append("FLASK_SECRET_KEY is required")
        
        if not self.AZURE_SPEECH_KEY:
            errors.append("AZURE_SPEECH_KEY is required")
        
        if not self.AZURE_SPEECH_REGION:
            errors.append("AZURE_SPEECH_REGION is required")
        
        # Blob storage validation (if enabled)
        if self.ENABLE_BLOB_STORAGE:
            if not self.AZURE_STORAGE_ACCOUNT_NAME:
                errors.append("AZURE_STORAGE_ACCOUNT_NAME is required when ENABLE_BLOB_STORAGE=true")
            
            if not self.USE_MANAGED_IDENTITY:
                if not self.AZURE_TENANT_ID:
                    errors.append("AZURE_TENANT_ID is required when using Service Principal authentication")
                if not self.AZURE_CLIENT_ID:
                    errors.append("AZURE_CLIENT_ID is required when using Service Principal authentication")
                if not self.AZURE_CLIENT_SECRET:
                    errors.append("AZURE_CLIENT_SECRET is required when using Service Principal authentication")
        
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
