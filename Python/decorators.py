"""
Decorators and context managers for the Speech-to-Text application
"""
import os
import tempfile
import logging
from contextlib import contextmanager
from typing import Generator, Optional, Callable, Any
from functools import wraps
from flask import send_file, Response

logger = logging.getLogger(__name__)


@contextmanager
def temporary_file(suffix: str = '', prefix: str = 'temp_', dir: Optional[str] = None) -> Generator[str, None, None]:
    """
    Context manager for creating and automatically cleaning up temporary files.
    
    Args:
        suffix: File suffix/extension (e.g., '.json', '.docx')
        prefix: File name prefix
        dir: Directory to create temp file in (None for system default)
        
    Yields:
        str: Path to the temporary file
        
    Example:
        with temporary_file(suffix='.json', prefix='transcription_') as temp_path:
            with open(temp_path, 'w') as f:
                f.write(json_data)
            # File is automatically deleted after the block
    """
    fd, temp_path = tempfile.mkstemp(suffix=suffix, prefix=prefix, dir=dir)
    os.close(fd)  # Close the file descriptor immediately
    
    try:
        yield temp_path
    finally:
        # Always clean up, even if an exception occurred
        try:
            if os.path.exists(temp_path):
                os.unlink(temp_path)
                logger.debug(f"Cleaned up temporary file: {temp_path}")
        except Exception as ex:
            logger.error(f"Failed to clean up temporary file {temp_path}: {ex}")


class TempFileResponse:
    """
    Helper class for creating Flask responses with automatic temp file cleanup.
    
    This class creates a response that sends a file and automatically cleans it up
    after the response is complete.
    """
    
    @staticmethod
    def create(
        content: str,
        filename: str,
        mimetype: str,
        suffix: str = '',
        prefix: str = 'temp_'
    ) -> Response:
        """
        Create a Flask send_file response with automatic cleanup.
        
        Args:
            content: The content to write to the file
            filename: The download filename presented to the user
            mimetype: MIME type of the file (e.g., 'application/json')
            suffix: File suffix/extension
            prefix: File name prefix
            
        Returns:
            Flask Response object configured for file download
            
        Raises:
            IOError: If file creation or writing fails
        """
        temp_path = None
        
        try:
            # Create temp file
            fd, temp_path = tempfile.mkstemp(suffix=suffix, prefix=prefix)
            
            try:
                # Write content
                with os.fdopen(fd, 'w', encoding='utf-8') as f:
                    f.write(content)
            except Exception:
                # If writing fails, close fd and clean up
                try:
                    os.close(fd)
                except:
                    pass
                if temp_path and os.path.exists(temp_path):
                    os.unlink(temp_path)
                raise
            
            # Create response
            response = send_file(
                temp_path,
                as_attachment=True,
                download_name=filename,
                mimetype=mimetype
            )
            
            # Add cleanup callback
            @response.call_on_close
            def cleanup():
                try:
                    if temp_path and os.path.exists(temp_path):
                        os.unlink(temp_path)
                        logger.debug(f"Cleaned up temp file: {temp_path}")
                except Exception as ex:
                    logger.error(f"Error cleaning up temp file {temp_path}: {ex}")
            
            return response
            
        except Exception:
            # Clean up on error
            if temp_path and os.path.exists(temp_path):
                try:
                    os.unlink(temp_path)
                except:
                    pass
            raise
    
    @staticmethod
    def create_binary(
        file_path: str,
        filename: str,
        mimetype: str,
        cleanup: bool = True
    ) -> Response:
        """
        Create a Flask send_file response for an existing file with optional cleanup.
        
        Args:
            file_path: Path to the existing file
            filename: The download filename presented to the user
            mimetype: MIME type of the file
            cleanup: Whether to delete the file after sending
            
        Returns:
            Flask Response object configured for file download
        """
        response = send_file(
            file_path,
            as_attachment=True,
            download_name=filename,
            mimetype=mimetype
        )
        
        if cleanup:
            @response.call_on_close
            def cleanup_file():
                try:
                    if os.path.exists(file_path):
                        os.unlink(file_path)
                        logger.debug(f"Cleaned up file: {file_path}")
                except Exception as ex:
                    logger.error(f"Error cleaning up file {file_path}: {ex}")
        
        return response


def cleanup_files_on_error(*file_paths_or_lists):
    """
    Decorator that cleans up specified files if the decorated function raises an exception.
    
    Args:
        *file_paths_or_lists: Variable arguments that can be:
            - String: Direct file path
            - List: List of file paths
            - Function parameter name (as string): Will extract from function kwargs
            
    Example:
        @cleanup_files_on_error('saved_file_paths')
        def process_files(saved_file_paths):
            # If this raises, all files in saved_file_paths are deleted
            ...
    """
    def decorator(func: Callable) -> Callable:
        @wraps(func)
        def wrapper(*args, **kwargs) -> Any:
            try:
                return func(*args, **kwargs)
            except Exception:
                # On error, clean up files
                for path_or_list in file_paths_or_lists:
                    # Check if it's a parameter name
                    if isinstance(path_or_list, str) and path_or_list in kwargs:
                        path_or_list = kwargs[path_or_list]
                    
                    # Handle list of paths
                    if isinstance(path_or_list, (list, tuple)):
                        paths = path_or_list
                    else:
                        paths = [path_or_list]
                    
                    # Clean up each path
                    for path in paths:
                        if path and isinstance(path, str):
                            try:
                                if os.path.exists(path):
                                    os.remove(path)
                                    logger.debug(f"Cleaned up file: {path}")
                            except Exception as cleanup_ex:
                                logger.error(f"Failed to clean up file {path}: {cleanup_ex}")
                
                # Re-raise the original exception
                raise
        
        return wrapper
    return decorator
