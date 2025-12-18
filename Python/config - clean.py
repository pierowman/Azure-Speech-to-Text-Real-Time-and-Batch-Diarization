"""
Configuration settings for the Speech-to-Text application
"""
import os
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


class Config:
    """Application configuration"""
    
    # Flask settings
    SECRET_KEY = os.getenv('FLASK_SECRET_KEY', 'dev-secret-key-change-in-production')
    DEBUG = os.getenv('FLASK_DEBUG', 'false').lower() == 'true'
    
    # Azure Speech Service
    AZURE_SPEECH_KEY = os.getenv('AZURE_SPEECH_KEY', '')
    AZURE_SPEECH_REGION = os.getenv('AZURE_SPEECH_REGION', '')
    AZURE_SPEECH_ENDPOINT = os.getenv('AZURE_SPEECH_ENDPOINT', '')
    
    # Azure Storage
    AZURE_STORAGE_ACCOUNT_NAME = os.getenv('AZURE_STORAGE_ACCOUNT_NAME', '')
    AZURE_STORAGE_CONTAINER_NAME = os.getenv('AZURE_STORAGE_CONTAINER_NAME', 'speech-transcriptions')
    ENABLE_BLOB_STORAGE = os.getenv('ENABLE_BLOB_STORAGE', 'false').lower() == 'true'
    USE_MANAGED_IDENTITY = os.getenv('USE_MANAGED_IDENTITY', 'false').lower() == 'true'
    AZURE_TENANT_ID = os.getenv('AZURE_TENANT_ID', '')
    AZURE_CLIENT_ID = os.getenv('AZURE_CLIENT_ID', '')
    AZURE_CLIENT_SECRET = os.getenv('AZURE_CLIENT_SECRET', '')
    
    # Upload settings
    UPLOAD_FOLDER = os.getenv('UPLOAD_FOLDER', 'static/uploads')
    MAX_CONTENT_LENGTH = int(os.getenv('MAX_CONTENT_LENGTH', 524288000))  # 500MB
    
    # Audio file extensions
    REALTIME_ALLOWED_EXTENSIONS = os.getenv('REALTIME_ALLOWED_EXTENSIONS', '.wav').split(',')
    BATCH_ALLOWED_EXTENSIONS = os.getenv('BATCH_ALLOWED_EXTENSIONS', 
                                          '.wav,.mp3,.ogg,.flac,.opus,.m4a,.webm').split(',')
    
    # Default settings
    DEFAULT_LOCALE = os.getenv('DEFAULT_LOCALE', 'en-US')
    DEFAULT_MIN_SPEAKERS = int(os.getenv('DEFAULT_MIN_SPEAKERS', 2))
    DEFAULT_MAX_SPEAKERS = int(os.getenv('DEFAULT_MAX_SPEAKERS', 10))
    
    # Batch job settings
    SHOW_TRANSCRIPTION_JOBS_TAB = os.getenv('SHOW_TRANSCRIPTION_JOBS_TAB', 'true').lower() == 'true'
    ENABLE_BATCH_TRANSCRIPTION = os.getenv('ENABLE_BATCH_TRANSCRIPTION', 'true').lower() == 'true'
    BATCH_JOB_AUTO_REFRESH_SECONDS = int(os.getenv('BATCH_JOB_AUTO_REFRESH_SECONDS', 10))
    
    # Cache settings
    LOCALES_CACHE_DURATION_HOURS = int(os.getenv('LOCALES_CACHE_DURATION_HOURS', 24))
    
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


config = Config()
