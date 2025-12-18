"""
Main Flask application for Azure Speech-to-Text with Diarization
"""
import os
import json
import logging
from datetime import datetime
from typing import Tuple, Dict, Any, Optional

from flask import Flask, request, jsonify, send_file, render_template, g, Response
from flask_wtf.csrf import CSRFProtect
from flask_limiter import Limiter
from flask_limiter.util import get_remote_address
from flask_caching import Cache
from werkzeug.utils import secure_filename
from werkzeug.datastructures import FileStorage
import uuid
import asyncio

from config import config
from models import SpeakerSegment, TranscriptionResult
from exceptions import (
    AppException, InvalidAudioFileException, TranscriptionException,
    ResourceNotFoundException, RateLimitException, AuthorizationException
)
from validators import audio_file_validator
from services.speech_service import speech_to_text_service
from services.batch_service import batch_transcription_service

# Import new helper modules
from decorators import temporary_file, TempFileResponse, cleanup_files_on_error
from document_generators import AuditLogDocumentGenerator, TranscriptionDocumentGenerator, CombinedDocumentGenerator
from route_helpers import (
    parse_segments_from_dict,
    assign_line_numbers,
    rebuild_transcript,
    validate_segment_update,
    create_audit_entry,
    build_segment_update_message,
    validate_json_request,
    validate_batch_transcription_params,
    generate_filename
)


# Custom logging filter to provide default request_id
class RequestIdFilter(logging.Filter):
    """Add request_id to log records, using 'startup' as default"""
    def filter(self, record) -> bool:
        try:
            from flask import g
            record.request_id = getattr(g, 'request_id', 'startup')
        except (RuntimeError, LookupError):
            # Outside of application context (during startup)
            record.request_id = 'startup'
        return True


# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - [%(request_id)s] - %(message)s'
)
logger = logging.getLogger(__name__)
logger.addFilter(RequestIdFilter())

# Add the filter to werkzeug's logger as well
werkzeug_logger = logging.getLogger('werkzeug')
werkzeug_logger.addFilter(RequestIdFilter())

# Create Flask app
app = Flask(__name__)
app.config.from_object(config)

# Security: CSRF Protection
csrf = CSRFProtect(app)

# Rate Limiting
limiter = Limiter(
    app=app,
    key_func=get_remote_address,
    default_limits=[f"{config.RATE_LIMIT_PER_MINUTE} per minute"],
    storage_uri="memory://"
)

# Caching
cache = Cache(app, config={
    'CACHE_TYPE': 'SimpleCache',
    'CACHE_DEFAULT_TIMEOUT': 86400  # 24 hours
})

# Ensure upload folder exists
os.makedirs(config.UPLOAD_FOLDER, exist_ok=True)


# ============================================================================
# MIDDLEWARE & ERROR HANDLERS
# ============================================================================

@app.before_request
def before_request() -> None:
    """Add request ID and logging context"""
    g.request_id = _generate_request_id()
    logger.info(f"Request started: {request.method} {request.path}", 
                extra={'request_id': g.request_id})


@app.after_request
def after_request(response: Response) -> Response:
    """Add security headers and log response"""
    # Security headers
    response.headers['X-Content-Type-Options'] = 'nosniff'
    response.headers['X-Frame-Options'] = 'DENY'
    response.headers['X-XSS-Protection'] = '1; mode=block'
    
    # Add request ID to response
    if hasattr(g, 'request_id'):
        response.headers['X-Request-ID'] = g.request_id
        logger.info(f"Request completed: {response.status_code}", 
                   extra={'request_id': g.request_id})
    
    return response


@app.errorhandler(AppException)
def handle_app_exception(e: AppException) -> Tuple[Response, int]:
    """Handle custom application exceptions"""
    logger.error(f"Application error: {e.error_code} - {e.message}", 
                extra={'request_id': getattr(g, 'request_id', 'unknown')})
    return jsonify(e.to_dict()), e.status_code


@app.errorhandler(429)
def handle_rate_limit(e: Exception) -> Tuple[Response, int]:
    """Handle rate limit errors"""
    return jsonify({
        'success': False,
        'error_code': 'RATE_LIMIT_EXCEEDED',
        'message': 'Too many requests. Please slow down.'
    }), 429


@app.errorhandler(Exception)
def handle_unexpected_error(e: Exception) -> Tuple[Response, int]:
    """Handle unexpected errors"""
    request_id = getattr(g, 'request_id', 'unknown')
    logger.error(f"Unexpected error: {str(e)}", exc_info=True,
                extra={'request_id': request_id})
    return jsonify({
        'success': False,
        'error_code': 'INTERNAL_ERROR',
        'message': 'An unexpected error occurred',
        'request_id': request_id
    }), 500


# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

def _generate_request_id() -> str:
    """Generate a unique request ID."""
    return str(uuid.uuid4())


def _save_uploaded_file(audio_file: FileStorage, upload_folder: str) -> str:
    """
    Save an uploaded file with a unique filename.
    
    Args:
        audio_file: Uploaded file from Flask request
        upload_folder: Directory to save file in
        
    Returns:
        Full path to saved file
    """
    ext = os.path.splitext(secure_filename(audio_file.filename))[1].lower()
    unique_filename = f"{uuid.uuid4()}{ext}"
    file_path = os.path.join(upload_folder, unique_filename)
    audio_file.save(file_path)
    return file_path


# ============================================================================
# ROUTES
# ============================================================================

@app.route('/')
@limiter.exempt
def index():
    """Render main page"""
    return render_template('index.html',
                         show_transcription_jobs_tab=config.SHOW_TRANSCRIPTION_JOBS_TAB,
                         enable_batch_transcription=config.ENABLE_BATCH_TRANSCRIPTION,
                         batch_job_auto_refresh_seconds=config.BATCH_JOB_AUTO_REFRESH_SECONDS,
                         realtime_allowed_extensions=config.REALTIME_ALLOWED_EXTENSIONS,
                         batch_allowed_extensions=config.BATCH_ALLOWED_EXTENSIONS,
                         default_min_speakers=config.DEFAULT_MIN_SPEAKERS,
                         default_max_speakers=config.DEFAULT_MAX_SPEAKERS,
                         default_locale=config.DEFAULT_LOCALE)


@app.route('/upload-and-transcribe', methods=['POST'])
@csrf.exempt  # TODO: Add CSRF token to form
def upload_and_transcribe() -> Response:
    """Upload audio file and perform real-time transcription"""
    try:
        # Get uploaded file
        if 'audioFile' not in request.files:
            raise InvalidAudioFileException('No file uploaded')
        
        audio_file = request.files['audioFile']
        
        # Validate file
        audio_file_validator.validate_file(audio_file, mode='realtime')
        
        # Save file
        file_path = _save_uploaded_file(audio_file, config.UPLOAD_FOLDER)
        logger.info(f"File uploaded: {file_path}")
        
        # Transcribe (locale is handled internally and hardcoded to en-US)
        result = speech_to_text_service.transcribe_with_diarization(file_path)
        
        # Add audio file URL
        unique_filename = os.path.basename(file_path)
        result.audio_file_url = f"/{config.UPLOAD_FOLDER}/{unique_filename}"
        
        # Create golden record (original)
        result.golden_record_json_data = json.dumps(result.to_dict(), indent=2)
        result.raw_json_data = json.dumps(result.to_dict(), indent=2)
        
        return jsonify(result.to_dict())
        
    except (InvalidAudioFileException, TranscriptionException) as ex:
        raise
    except Exception as ex:
        logger.error(f"Unexpected error in upload_and_transcribe: {ex}", exc_info=True)
        raise TranscriptionException(str(ex))


@app.route('/update-speaker-names', methods=['POST'])
@csrf.exempt
def update_speaker_names() -> Response:
    """
    Update speaker names across multiple segments.
    Creates audit log entries for bulk speaker operations.
    
    Request JSON:
        segments: List of segment dictionaries (with ORIGINAL speaker values)
        audioFileUrl: Path to audio file
        goldenRecordJsonData: Original transcription data
        auditLog: Current audit log array
        availableSpeakers: List of all available speakers (including those with 0 segments)
        oldSpeaker: (Optional) Original speaker name being changed
        newSpeaker: (Optional) New speaker name
        operationType: (Optional) Type of operation: 'rename', 'reassign', or 'delete'
    """
    try:
        data = request.get_json()
        error_msg = validate_json_request(data, ['segments'])
        if error_msg:
            raise InvalidAudioFileException(error_msg)
        
        segments = parse_segments_from_dict(data['segments'])
        assign_line_numbers(segments)
        
        # Get audit log, availableSpeakers, and operation details
        audit_log = data.get('auditLog', [])
        available_speakers = data.get('availableSpeakers', [])
        old_speaker = data.get('oldSpeaker')
        new_speaker = data.get('newSpeaker')
        operation_type = data.get('operationType')  # 'rename', 'reassign', or 'delete'
        
        logger.info(f"?? DEBUG: Received speaker update request")
        logger.info(f"?? DEBUG: Segments count: {len(segments)}")
        logger.info(f"?? DEBUG: Available speakers: {available_speakers}")
        logger.info(f"?? DEBUG: Current audit log entries: {len(audit_log)}")
        logger.info(f"?? DEBUG: Operation: {operation_type or 'none'} - '{old_speaker}' ? '{new_speaker}'")
        
        # ?? If explicit speaker change provided, create audit entry
        if old_speaker and new_speaker and operation_type:
            # Count affected segments and collect their indices
            affected_segments = []
            segment_count = 0
            
            for i, segment in enumerate(segments):
                if segment.speaker == old_speaker:
                    # Apply the speaker change to the segment
                    segment.speaker = new_speaker
                    affected_segments.append(i)
                    segment_count += 1
            
            logger.info(f"?? DEBUG: Found {segment_count} segments to update")
            
            # Update availableSpeakers list if operation affects it
            if operation_type == 'rename' and old_speaker in available_speakers:
                # Replace old speaker name with new name in availableSpeakers
                available_speakers = [new_speaker if s == old_speaker else s for s in available_speakers]
                available_speakers = sorted(list(set(available_speakers)))  # Remove duplicates and sort
                logger.info(f"?? Updated availableSpeakers after rename: {available_speakers}")
            
            if segment_count > 0:
                # Create audit entry
                action = f'bulk_speaker_{operation_type}'  # e.g., 'bulk_speaker_rename'
                
                if operation_type == 'rename':
                    description = f"Renamed speaker \"{old_speaker}\" to \"{new_speaker}\" across {segment_count} segment(s)"
                elif operation_type == 'delete':
                    description = f"Deleted speaker \"{old_speaker}\" and reassigned {segment_count} segment(s) to \"{new_speaker}\""
                    # Remove deleted speaker from availableSpeakers
                    if old_speaker in available_speakers:
                        available_speakers.remove(old_speaker)
                        logger.info(f"?? Removed '{old_speaker}' from availableSpeakers after delete")
                else:  # reassign
                    description = f"Reassigned {segment_count} segment(s) from \"{old_speaker}\" to \"{new_speaker}\""
                
                audit_entry = {
                    'timestamp': datetime.now().isoformat(),
                    'action': action,
                    'segmentCount': segment_count,
                    'affectedSegments': affected_segments,
                    'oldSpeaker': old_speaker,
                    'newSpeaker': new_speaker,
                    'description': description
                }
                
                audit_log.append(audit_entry)
                logger.info(f"? Created audit entry: {action} - {old_speaker} ? {new_speaker} ({segment_count} segments)")
        
        logger.info(f"?? DEBUG: Final audit log entries: {len(audit_log)}")
        logger.info(f"?? DEBUG: Final available speakers: {available_speakers}")
        
        # Rebuild transcript with updated speakers
        # ?? CRITICAL: rebuild_transcript() recalculates availableSpeakers from segments only,
        # which would lose speakers with 0 segments. We must use the availableSpeakers from the request.
        transcript_data = rebuild_transcript(segments)
        
        # ?? IMPORTANT: Override the auto-calculated availableSpeakers with our maintained list
        # This preserves speakers with 0 segments (like newly added speakers)
        logger.info(f"?? DEBUG: rebuild_transcript returned availableSpeakers: {transcript_data.get('availableSpeakers', [])}")
        transcript_data['availableSpeakers'] = available_speakers
        logger.info(f"?? DEBUG: Overriding with maintained availableSpeakers: {available_speakers}")
        
        result = {
            'success': True,
            'message': f'Updated speaker names for {len(segments)} segments',
            'segments': [s.to_dict() for s in segments],
            'availableSpeakers': available_speakers,  # ?? Include in response
            **transcript_data,
            'audioFileUrl': data.get('audioFileUrl'),
            'goldenRecordJsonData': data.get('goldenRecordJsonData'),
            'auditLog': audit_log
        }
        
        result['rawJsonData'] = json.dumps(result, indent=2)
        
        logger.info(f"? Returning response with {len(audit_log)} audit entries and {len(available_speakers)} available speakers")
        return jsonify(result)
        
    except Exception as ex:
        logger.error(f"Error updating speaker names: {ex}", exc_info=True)
        raise TranscriptionException('Error updating speaker names')


@app.route('/update-segment-text', methods=['POST'])
@csrf.exempt
def update_segment_text() -> Response:
    """Update segment text and/or speaker with audit logging"""
    try:
        data = request.get_json()
        
        # Validate request
        error_msg = validate_json_request(data, ['segmentIndex', 'segments'])
        if error_msg:
            raise InvalidAudioFileException(error_msg)
        
        segment_index = data.get('segmentIndex')
        new_text = data.get('newText', '')
        new_speaker = data.get('newSpeaker')
        segments_data = data.get('segments', [])
        audit_log = data.get('auditLog', [])
        
        # Get the segment being edited
        if segment_index < 0 or segment_index >= len(segments_data):
            raise InvalidAudioFileException('Invalid segment index')
            
        segment_data = segments_data[segment_index]
        old_text = segment_data.get('text', '')
        old_speaker = segment_data.get('speaker', '')
        
        # Validate changes
        text_changed, speaker_changed, validation_error = validate_segment_update(
            segment_index, new_text, old_text, new_speaker, old_speaker, len(segments_data)
        )
        
        if validation_error:
            raise InvalidAudioFileException(validation_error)
        
        # ? CRITICAL: Preserve original values on FIRST edit
        # This allows the Word document to show [EDITED] badge for segments
        # that were individually edited (not bulk operations)
        
        # Preserve original text if this is the first text edit
        if text_changed and not segment_data.get('originalText'):
            segment_data['originalText'] = old_text
            logger.info(f"?? Set originalText for segment {segment_index}: '{old_text}'")
        
        # Preserve original speaker if this is the first speaker edit
        if speaker_changed and not segment_data.get('originalSpeaker'):
            segment_data['originalSpeaker'] = old_speaker
            logger.info(f"?? Set originalSpeaker for segment {segment_index}: '{old_speaker}'")
        
        # Update segment data with new values
        segment_data['text'] = new_text
        if speaker_changed:
            segment_data['speaker'] = new_speaker
        
        # Create audit entry
        audit_entry = create_audit_entry(
            segment_data, segment_index, old_text, new_text,
            old_speaker, new_speaker, text_changed, speaker_changed
        )
        audit_log.append(audit_entry)
        
        # Parse all segments
        segments = parse_segments_from_dict(segments_data)
        
        # Rebuild transcript and statistics
        transcript_data = rebuild_transcript(segments)
        
        # Build success message
        message = build_segment_update_message(
            segment_data.get('lineNumber'), speaker_changed, text_changed, new_speaker
        )
        
        # Build result
        result = {
            'success': True,
            'message': message,
            'segments': [s.to_dict() for s in segments],
            **transcript_data,
            'audioFileUrl': data.get('audioFileUrl'),
            'goldenRecordJsonData': data.get('goldenRecordJsonData'),
            'auditLog': audit_log,
            'lastEdit': audit_entry
        }

        # Serialize
        result['rawJsonData'] = json.dumps(result, indent=2)
        
        return jsonify(result)
        
    except (InvalidAudioFileException, TranscriptionException) as ex:
        raise
    except Exception as ex:
        logger.error(f"Error updating segment: {ex}", exc_info=True)
        raise TranscriptionException('Error updating segment')


@app.route('/download-audit-log', methods=['POST'])
@csrf.exempt
def download_audit_log() -> Response:
    """Download edit audit log as Word document"""
    try:
        data = request.get_json()
        
        # Validate request
        error_msg = validate_json_request(data, ['auditLog'])
        if error_msg:
            raise InvalidAudioFileException(error_msg)
        
        audit_log = data.get('auditLog', [])
        if not audit_log:
            raise InvalidAudioFileException('No audit log data')
        
        # Generate document
        doc = AuditLogDocumentGenerator.create_document(audit_log)
        
        # Save to temp file and create response
        filename = generate_filename('transcription_audit_log_', '.docx')
        
        with temporary_file(suffix='.docx', prefix='audit_') as temp_path:
            doc.save(temp_path)
            return TempFileResponse.create_binary(
                temp_path,
                filename,
                'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
                cleanup=False  # Will be cleaned up by context manager
            )
        
    except Exception as ex:
        logger.error(f"Error generating audit log download: {ex}", exc_info=True)
        raise TranscriptionException('Error generating download')


@app.route('/download-raw-json', methods=['POST'])
@csrf.exempt
def download_raw_json() -> Response:
    """Download raw JSON data"""
    try:
        data = request.get_json()
        
        # Validate request
        error_msg = validate_json_request(data, ['jsonData'])
        if error_msg:
            raise InvalidAudioFileException(error_msg)
        
        json_data = data.get('jsonData', '')
        if not json_data:
            raise InvalidAudioFileException('No data provided')
        
        filename = generate_filename('transcription_raw_', '.json')
        
        return TempFileResponse.create(
            content=json_data,
            filename=filename,
            mimetype='application/json',
            suffix='.json',
            prefix='transcription_'
        )
        
    except Exception as ex:
        logger.error(f"Error generating download: {ex}")
        raise TranscriptionException('Error generating download')


@app.route('/download-golden-record', methods=['POST'])
@csrf.exempt
def download_golden_record() -> Response:
    """Download golden record JSON data"""
    try:
        data = request.get_json()
        
        # Validate request
        error_msg = validate_json_request(data, ['goldenRecordJsonData'])
        if error_msg:
            raise InvalidAudioFileException(error_msg)
        
        json_data = data.get('goldenRecordJsonData', '')
        if not json_data:
            raise InvalidAudioFileException('No data provided')
        
        filename = generate_filename('transcription_original_', '.json')
        
        return TempFileResponse.create(
            content=json_data,
            filename=filename,
            mimetype='application/json',
            suffix='.json',
            prefix='transcription_'
        )
        
    except Exception as ex:
        logger.error(f"Error generating download: {ex}")
        raise TranscriptionException('Error generating download')


@app.route('/download-readable-text', methods=['POST'])
@csrf.exempt
def download_readable_text() -> Response:
    """Download transcription as formatted Word document"""
    try:
        data = request.get_json()
        
        # Validate request
        error_msg = validate_json_request(data, ['segments'])
        if error_msg:
            raise InvalidAudioFileException(error_msg)
        
        segments = data.get('segments', [])
        if not segments:
            raise InvalidAudioFileException('No segments provided')
        
        # Generate document
        doc = TranscriptionDocumentGenerator.create_document(segments)
        
        # Save to temp file and create response
        filename = generate_filename('transcription_', '.docx')
        
        with temporary_file(suffix='.docx', prefix='transcription_') as temp_path:
            doc.save(temp_path)
            return TempFileResponse.create_binary(
                temp_path,
                filename,
                'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
                cleanup=False  # Will be cleaned up by context manager
            )
        
    except Exception as ex:
        logger.error(f"Error generating Word document: {ex}", exc_info=True)
        raise TranscriptionException('Error generating download')


@app.route('/download-combined-document', methods=['POST'])
@csrf.exempt
def download_combined_document() -> Response:
    """Download combined transcription and audit log as formatted Word document"""
    try:
        data = request.get_json()
        
        # Validate request
        error_msg = validate_json_request(data, ['segments'])
        if error_msg:
            raise InvalidAudioFileException(error_msg)
        
        segments = data.get('segments', [])
        if not segments:
            raise InvalidAudioFileException('No segments provided')
        
        audit_log = data.get('auditLog', [])
        
        # Generate combined document
        doc = CombinedDocumentGenerator.create_document(segments, audit_log)
        
        # Save to temp file and create response
        filename = generate_filename('transcription_with_history_', '.docx')
        
        with temporary_file(suffix='.docx', prefix='combined_') as temp_path:
            doc.save(temp_path)
            return TempFileResponse.create_binary(
                temp_path,
                filename,
                'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
                cleanup=False  # Will be cleaned up by context manager
            )
        
    except Exception as ex:
        logger.error(f"Error generating combined document: {ex}", exc_info=True)
        raise TranscriptionException('Error generating download')


@app.route('/validation-rules/<mode>')
@limiter.exempt
def get_validation_rules(mode: str) -> Response:
    """Get validation rules for a transcription mode"""
    try:
        rules = audio_file_validator.get_validation_rules_summary(mode)
        return jsonify({'success': True, 'rules': rules})
    except Exception as ex:
        logger.error(f"Error getting validation rules: {ex}")
        raise TranscriptionException(str(ex))


@app.route('/supported-locales')
@limiter.exempt
@cache.cached(timeout=86400)  # Cache for 24 hours
def get_supported_locales() -> Response:
    """Get list of supported locales"""
    try:
        locales = batch_transcription_service.get_supported_locales()
        return jsonify({
            'success': True,
            'locales': locales,
            'count': len(locales)
        })
    except Exception as ex:
        logger.error(f"Error fetching locales: {ex}")
        raise TranscriptionException(str(ex))


@app.route('/supported-locales-with-names')
@limiter.exempt
@cache.cached(timeout=86400)  # Cache for 24 hours
def get_supported_locales_with_names() -> Response:
    """Get list of supported locales with display names"""
    try:
        locales = batch_transcription_service.get_locale_names()
        return jsonify({
            'success': True,
            'locales': [l.to_dict() for l in locales],
            'count': len(locales)
        })
    except Exception as ex:
        logger.error(f"Error fetching locales: {ex}")
        raise TranscriptionException(str(ex))


@app.route('/create-batch-transcription', methods=['POST'])
@csrf.exempt
def create_batch_transcription() -> Response:
    """Create a batch transcription job"""
    saved_file_paths = []
    
    try:
        if not config.ENABLE_BATCH_TRANSCRIPTION:
            raise AuthorizationException('Batch transcription is disabled in configuration')
        
        if 'audioFiles' not in request.files:
            raise InvalidAudioFileException('No files uploaded')
        
        audio_files = request.files.getlist('audioFiles')
        if not audio_files or len(audio_files) == 0:
            raise InvalidAudioFileException('No files uploaded')
        
        # Get and validate form parameters
        job_name = request.form.get('jobName', f"Batch Job {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        locale = request.form.get('locale', config.DEFAULT_LOCALE)
        
        enable_diarization, min_speakers, max_speakers, validation_error = validate_batch_transcription_params(
            request.form.get('enableDiarization', 'true'),
            request.form.get('minSpeakers', str(config.DEFAULT_MIN_SPEAKERS)),
            request.form.get('maxSpeakers', str(config.DEFAULT_MAX_SPEAKERS))
        )
        
        if validation_error:
            raise InvalidAudioFileException(validation_error)
        
        # Validate and save files
        saved_file_paths = _process_batch_files(audio_files)
        
        # Create batch job using asyncio
        job = asyncio.run(
            batch_transcription_service.create_batch_transcription(
                audio_file_paths=saved_file_paths,
                job_name=job_name,
                language=locale,
                enable_diarization=enable_diarization,
                min_speakers=min_speakers,
                max_speakers=max_speakers
            )
        )
        
        logger.info(f"Batch transcription job created: {job.id}")
        
        # Clean up uploaded files after successful job creation
        _cleanup_files(saved_file_paths)
        
        return jsonify({
            'success': True,
            'message': f'Batch job created successfully with {len(saved_file_paths)} file(s)',
            'job': job.to_dict()
        })
        
    except (InvalidAudioFileException, AuthorizationException) as ex:
        _cleanup_files(saved_file_paths)
        raise
    except Exception as ex:
        logger.error(f"Error creating batch transcription: {ex}", exc_info=True)
        _cleanup_files(saved_file_paths)
        raise TranscriptionException(f'Failed to create batch job: {str(ex)}')


def _process_batch_files(audio_files: list) -> list:
    """
    Process and save batch audio files.
    
    Args:
        audio_files: List of uploaded files
        
    Returns:
        List of saved file paths
        
    Raises:
        InvalidAudioFileException: If any file is invalid
    """
    saved_file_paths = []
    
    try:
        for audio_file in audio_files:
            audio_file_validator.validate_file(audio_file, mode='batch')
            file_path = _save_uploaded_file(audio_file, config.UPLOAD_FOLDER)
            saved_file_paths.append(file_path)
            logger.info(f"Batch file uploaded: {file_path}")
        
        return saved_file_paths
        
    except Exception as ex:
        # Clean up any files saved before the error
        _cleanup_files(saved_file_paths)
        raise InvalidAudioFileException(f'Invalid file: {str(ex)}')


def _cleanup_files(file_paths: list) -> None:
    """
    Clean up a list of files.
    
    Args:
        file_paths: List of file paths to delete
    """
    for path in file_paths:
        try:
            if path and os.path.exists(path):
                os.remove(path)
                logger.debug(f"Cleaned up file: {path}")
        except Exception as cleanup_ex:
            logger.error(f"Failed to clean up file {path}: {cleanup_ex}")


@app.route('/batch-jobs', methods=['GET', 'POST'])
@csrf.exempt
def get_batch_jobs() -> Response:
    """Get list of batch transcription jobs with optional caching support"""
    try:
        if not config.ENABLE_BATCH_TRANSCRIPTION:
            raise AuthorizationException('Batch transcription is disabled')
        
        skip = int(request.args.get('skip', 0))
        top = int(request.args.get('top', 100))
        
        cached_jobs = None
        force_refresh = False
        if request.method == 'POST':
            try:
                data = request.get_json()
                cached_jobs = data.get('cachedJobs', None)
                force_refresh = data.get('forceRefresh', False)
                if cached_jobs:
                    logger.info(f"Received {len(cached_jobs)} cached jobs from client (forceRefresh={force_refresh})")
            except:
                pass
        
        if cached_jobs and not force_refresh:
            cached_job_ids = {job.get('id') for job in cached_jobs}
            logger.info(f"Using optimized refresh with {len(cached_jobs)} cached jobs")
        
        jobs = asyncio.run(
            batch_transcription_service.get_transcription_jobs(
                skip=skip, 
                top=top, 
                cached_jobs=cached_jobs,
                force_refresh=force_refresh
            )
        )
        
        return jsonify({
            'success': True,
            'jobs': [job.to_dict() for job in jobs],
            'count': len(jobs)
        })
        
    except AuthorizationException as ex:
        raise
    except Exception as ex:
        logger.error(f"Error fetching batch jobs: {ex}", exc_info=True)
        raise TranscriptionException(str(ex))


@app.route('/batch-job/<job_id>', methods=['GET'])
@csrf.exempt
def get_batch_job_status(job_id: str) -> Response:
    """Get status of a specific batch job"""
    try:
        if not config.ENABLE_BATCH_TRANSCRIPTION:
            raise AuthorizationException('Batch transcription is disabled')
        
        job = asyncio.run(
            batch_transcription_service.get_transcription_job_status(job_id)
        )
        
        if not job:
            raise ResourceNotFoundException(f'Job {job_id} not found')
        
        return jsonify({
            'success': True,
            'job': job.to_dict()
        });
        
    except (AuthorizationException, ResourceNotFoundException) as ex:
        raise
    except Exception as ex:
        logger.error(f"Error fetching job status: {ex}", exc_info=True)
        raise TranscriptionException(str(ex))


@app.route('/batch-job/<job_id>/files', methods=['GET'])
@csrf.exempt
def get_batch_job_files(job_id: str) -> Response:
    """Get list of available transcription files for a batch job"""
    try:
        if not config.ENABLE_BATCH_TRANSCRIPTION:
            raise AuthorizationException('Batch transcription is disabled')
        
        files = asyncio.run(
            batch_transcription_service.get_transcription_files_list(job_id)
        )
        
        return jsonify({
            'success': True,
            'files': files,
            'count': len(files)
        })
        
    except AuthorizationException as ex:
        raise
    except Exception as ex:
        logger.error(f"Error fetching job files: {ex}", exc_info=True)
        raise TranscriptionException(str(ex))


@app.route('/batch-job/<job_id>/results', methods=['GET', 'POST'])
@csrf.exempt
def get_batch_job_results(job_id: str) -> Response:
    """Get transcription results for a completed batch job"""
    try:
        if not config.ENABLE_BATCH_TRANSCRIPTION:
            raise AuthorizationException('Batch transcription is disabled')
        
        file_indices = None
        if request.method == 'POST':
            data = request.get_json()
            file_indices = data.get('fileIndices', None)
            logger.info(f"Processing {len(file_indices) if file_indices else 0} selected file(s) by index")
        
        result = asyncio.run(
            batch_transcription_service.get_transcription_results(job_id, file_indices)
        )
        
        if not result:
            raise ResourceNotFoundException(f'Results for job {job_id} not found')
        
        return jsonify(result.to_dict())
        
    except (AuthorizationException, ResourceNotFoundException) as ex:
        raise
    except Exception as ex:
        logger.error(f"Error fetching job results: {ex}", exc_info=True)
        raise TranscriptionException(str(ex))


@app.route('/batch-job/<job_id>', methods=['DELETE'])
@csrf.exempt
def delete_batch_job(job_id: str) -> Response:
    """Delete a batch transcription job"""
    try:
        if not config.ENABLE_BATCH_TRANSCRIPTION:
            raise AuthorizationException('Batch transcription is disabled')
        
        success = asyncio.run(
            batch_transcription_service.delete_transcription_job(job_id)
        )
        
        if success:
            return jsonify({
                'success': True,
                'message': f'Job {job_id} deleted successfully'
            })
        else:
            raise TranscriptionException(f'Failed to delete job {job_id}')
        
    except AuthorizationException as ex:
        raise
    except Exception as ex:
        logger.error(f"Error deleting job: {ex}", exc_info=True)
        raise TranscriptionException(str(ex))


if __name__ == '__main__':
    # Log startup information
    logger.info("=== ENVIRONMENT CONFIGURATION ===")
    logger.info(f"Debug Mode: {config.DEBUG}")
    logger.info(f"Upload Folder: {config.UPLOAD_FOLDER}")
    logger.info(f"Default Locale: {config.DEFAULT_LOCALE}")
    logger.info(f"Azure Speech Region: {config.AZURE_SPEECH_REGION}")
    
    logger.info("=== AZURE STORAGE CONFIGURATION ===")
    logger.info(f"Enable Blob Storage: {config.ENABLE_BLOB_STORAGE}")
    logger.info(f"Storage Account: {config.AZURE_STORAGE_ACCOUNT_NAME or '<EMPTY>'}")
    logger.info(f"Container Name: {config.AZURE_STORAGE_CONTAINER_NAME}")
    logger.info(f"Is Configured: {config.IS_CONFIGURED}")
    
    # Run app
    app.run(debug=config.DEBUG, host='0.0.0.0', port=5000)
