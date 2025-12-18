"""
Custom exceptions for the Speech-to-Text application
"""


class AppException(Exception):
    """Base exception for all application errors"""
    status_code = 500
    error_code = "INTERNAL_ERROR"
    
    def __init__(self, message: str = None, **kwargs):
        self.message = message or self.get_default_message()
        self.details = kwargs
        super().__init__(self.message)
    
    def get_default_message(self) -> str:
        """Get default error message"""
        return "An internal error occurred"
    
    def to_dict(self):
        """Convert exception to dictionary for JSON response"""
        result = {
            'success': False,
            'error_code': self.error_code,
            'message': self.message
        }
        if self.details:
            result['details'] = self.details
        return result


class TranscriptionException(AppException):
    """Exception raised during transcription processing"""
    status_code = 500
    error_code = "TRANSCRIPTION_ERROR"
    
    def get_default_message(self) -> str:
        return "Transcription processing failed"


class InvalidAudioFileException(AppException):
    """Exception raised when an audio file is invalid"""
    status_code = 400
    error_code = "INVALID_AUDIO_FILE"
    
    def get_default_message(self) -> str:
        return "Invalid audio file"


class ValidationException(AppException):
    """Exception raised during file validation"""
    status_code = 400
    error_code = "VALIDATION_ERROR"
    
    def get_default_message(self) -> str:
        return "Validation failed"


class AuthenticationException(AppException):
    """Exception raised during authentication"""
    status_code = 401
    error_code = "AUTHENTICATION_ERROR"
    
    def get_default_message(self) -> str:
        return "Authentication failed"


class AuthorizationException(AppException):
    """Exception raised when user lacks permissions"""
    status_code = 403
    error_code = "AUTHORIZATION_ERROR"
    
    def get_default_message(self) -> str:
        return "Access denied"


class ResourceNotFoundException(AppException):
    """Exception raised when a resource is not found"""
    status_code = 404
    error_code = "RESOURCE_NOT_FOUND"
    
    def get_default_message(self) -> str:
        return "Resource not found"


class RateLimitException(AppException):
    """Exception raised when rate limit is exceeded"""
    status_code = 429
    error_code = "RATE_LIMIT_EXCEEDED"
    
    def get_default_message(self) -> str:
        return "Rate limit exceeded. Please try again later."


class AzureServiceException(AppException):
    """Exception raised when Azure service call fails"""
    status_code = 502
    error_code = "AZURE_SERVICE_ERROR"
    
    def get_default_message(self) -> str:
        return "Azure service error"


class StorageException(AppException):
    """Exception raised during storage operations"""
    status_code = 500
    error_code = "STORAGE_ERROR"
    
    def get_default_message(self) -> str:
        return "Storage operation failed"


class ConfigurationException(AppException):
    """Exception raised for configuration errors"""
    status_code = 500
    error_code = "CONFIGURATION_ERROR"
    
    def get_default_message(self) -> str:
        return "Configuration error"
