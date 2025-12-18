"""
Audio file validation service
"""
import os
from typing import List
from werkzeug.datastructures import FileStorage
from exceptions import InvalidAudioFileException
from config import config


class AudioFileValidator:
    """Validates audio files for transcription"""
    
    # MIME types for audio files
    AUDIO_MIME_TYPES = {
        '.wav': ['audio/wav', 'audio/x-wav', 'audio/wave'],
        '.mp3': ['audio/mpeg', 'audio/mp3'],
        '.ogg': ['audio/ogg', 'application/ogg'],
        '.flac': ['audio/flac', 'audio/x-flac'],
        '.opus': ['audio/opus'],
        '.m4a': ['audio/mp4', 'audio/x-m4a'],
        '.webm': ['audio/webm', 'video/webm']
    }
    
    def __init__(self):
        # Load configuration values
        self.realtime_extensions = [ext.lower() for ext in config.REALTIME_ALLOWED_EXTENSIONS]
        self.batch_extensions = [ext.lower() for ext in config.BATCH_ALLOWED_EXTENSIONS]
        self.realtime_max_size = config.REALTIME_MAX_FILE_SIZE
        self.batch_max_size = config.BATCH_MAX_FILE_SIZE
        self.batch_max_files = config.BATCH_MAX_FILES
        
        # Try to import python-magic for content validation
        self.magic_available = False
        try:
            import magic
            self.magic = magic
            self.magic_available = True
        except ImportError:
            pass
    
    def validate_file(self, file: FileStorage, mode: str = 'realtime') -> None:
        """
        Validate a single audio file
        
        Args:
            file: The uploaded file
            mode: 'realtime' or 'batch'
            
        Raises:
            InvalidAudioFileException: If validation fails
        """
        if not file or not file.filename:
            raise InvalidAudioFileException("No file provided")
        
        # Check file extension
        ext = os.path.splitext(file.filename)[1].lower()
        allowed_extensions = self.realtime_extensions if mode == 'realtime' else self.batch_extensions
        
        if ext not in allowed_extensions:
            ext_list = ', '.join(allowed_extensions)
            raise InvalidAudioFileException(
                f"Invalid file type '{ext}'. Allowed types for {mode} mode: {ext_list}"
            )
        
        # Check file size - ensure file pointer is always reset
        original_position = file.tell()
        try:
            # Seek to end to get size
            file.seek(0, os.SEEK_END)
            file_size = file.tell()
            
            max_size = self.realtime_max_size if mode == 'realtime' else self.batch_max_size
            
            if file_size == 0:
                raise InvalidAudioFileException("File is empty")
            
            if file_size > max_size:
                max_size_mb = max_size / (1024 * 1024)
                file_size_mb = file_size / (1024 * 1024)
                raise InvalidAudioFileException(
                    f"File size ({file_size_mb:.1f} MB) exceeds maximum "
                    f"allowed size ({max_size_mb:.0f} MB) for {mode} mode"
                )
            
            # Validate file content (MIME type)
            if self.magic_available:
                file.seek(0)
                self._validate_file_content(file, ext)
            
        finally:
            # Always reset file pointer to original position
            file.seek(original_position)
    
    def _validate_file_content(self, file: FileStorage, ext: str) -> None:
        """
        Validate file content matches expected MIME type
        
        Args:
            file: The uploaded file (pointer at start)
            ext: File extension
            
        Raises:
            InvalidAudioFileException: If content doesn't match extension
        """
        try:
            # Read first 2KB for MIME detection
            sample = file.read(2048)
            file.seek(0)  # Reset after reading
            
            # Detect MIME type
            mime_type = self.magic.from_buffer(sample, mime=True)
            
            # Check if MIME type matches extension
            expected_mimes = self.AUDIO_MIME_TYPES.get(ext, [])
            
            if expected_mimes and mime_type not in expected_mimes:
                # Allow some flexibility for common mismatches
                if not (mime_type.startswith('audio/') or mime_type.startswith('video/')):
                    raise InvalidAudioFileException(
                        f"File content type '{mime_type}' doesn't match extension '{ext}'. "
                        f"The file may be corrupted or renamed."
                    )
        
        except Exception as e:
            # Don't fail validation if content check fails, just log warning
            import logging
            logger = logging.getLogger(__name__)
            logger.warning(f"Content validation failed: {e}")
    
    def validate_files(self, files: List[FileStorage]) -> None:
        """
        Validate multiple audio files for batch transcription
        
        Args:
            files: List of uploaded files
            
        Raises:
            InvalidAudioFileException: If validation fails
        """
        if not files:
            raise InvalidAudioFileException("No files provided")
        
        if len(files) > self.batch_max_files:
            raise InvalidAudioFileException(
                f"Too many files. Maximum {self.batch_max_files} files allowed per batch."
            )
        
        # Validate each file
        for i, file in enumerate(files, 1):
            try:
                self.validate_file(file, mode='batch')
            except InvalidAudioFileException as e:
                raise InvalidAudioFileException(f"File {i} ({file.filename}): {str(e)}")
    
    def get_validation_rules_summary(self, mode: str = 'realtime') -> dict:
        """
        Get validation rules for display
        
        Args:
            mode: 'realtime' or 'batch'
            
        Returns:
            Dictionary containing validation rules
        """
        if mode == 'realtime':
            return {
                'mode': 'realtime',
                'allowedExtensions': self.realtime_extensions,
                'maxFileSize': self.realtime_max_size,
                'maxFileSizeMB': self.realtime_max_size / (1024 * 1024),
                'maxFiles': 1,
                'contentValidationEnabled': self.magic_available
            }
        else:
            return {
                'mode': 'batch',
                'allowedExtensions': self.batch_extensions,
                'maxFileSize': self.batch_max_size,
                'maxFileSizeMB': self.batch_max_size / (1024 * 1024),
                'maxFiles': self.batch_max_files,
                'contentValidationEnabled': self.magic_available
            }


# Create a global instance
audio_file_validator = AudioFileValidator()

