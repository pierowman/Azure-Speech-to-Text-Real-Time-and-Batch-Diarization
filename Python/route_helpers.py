"""
Route handler helper functions for the Speech-to-Text application.
This module contains business logic extracted from route handlers to improve
maintainability and testability.
"""
import json
import logging
from typing import List, Dict, Any, Tuple, Optional
from datetime import datetime

from models import SpeakerSegment, SpeakerInfo

logger = logging.getLogger(__name__)


def parse_segments_from_dict(segments_data: List[Dict[str, Any]]) -> List[SpeakerSegment]:
    """
    Parse segment dictionaries into SpeakerSegment objects.
    
    Args:
        segments_data: List of segment dictionaries from JSON
        
    Returns:
        List of SpeakerSegment objects
    """
    segments = []
    for seg_data in segments_data:
        segment = SpeakerSegment(
            speaker=seg_data.get('speaker', ''),
            text=seg_data.get('text', ''),
            offset_in_ticks=seg_data.get('offsetInTicks', 0),
            duration_in_ticks=seg_data.get('durationInTicks', 0),
            line_number=seg_data.get('lineNumber', 0),
            original_speaker=seg_data.get('originalSpeaker'),
            original_text=seg_data.get('originalText')
        )
        segments.append(segment)
    return segments


def assign_line_numbers(segments: List[SpeakerSegment]) -> None:
    """
    Assign sequential line numbers to segments.
    
    Args:
        segments: List of SpeakerSegment objects (modified in place)
    """
    for i, segment in enumerate(segments, 1):
        segment.line_number = i


def build_full_transcript(segments: List[SpeakerSegment]) -> str:
    """
    Build a formatted transcript string from segments.
    
    Args:
        segments: List of SpeakerSegment objects
        
    Returns:
        Formatted transcript string with speaker labels
    """
    transcript_lines = [f"[{s.speaker}]: {s.text}" for s in segments]
    return "\n".join(transcript_lines)


def calculate_speaker_statistics(segments: List[SpeakerSegment]) -> Tuple[List[str], List[SpeakerInfo]]:
    """
    Calculate available speakers and their statistics.
    
    Args:
        segments: List of SpeakerSegment objects
        
    Returns:
        Tuple of (available_speakers, speaker_statistics)
        - available_speakers: Sorted list of unique speaker names
        - speaker_statistics: List of SpeakerInfo objects with stats
    """
    # Calculate available speakers
    available_speakers = sorted(list(set(
        s.speaker for s in segments if s.speaker.strip()
    )))
    
    # Group segments by speaker
    speaker_groups: Dict[str, List[SpeakerSegment]] = {}
    for segment in segments:
        if segment.speaker not in speaker_groups:
            speaker_groups[segment.speaker] = []
        speaker_groups[segment.speaker].append(segment)
    
    # Calculate statistics for each speaker
    speaker_statistics = []
    for speaker, speaker_segments in speaker_groups.items():
        total_time = sum(
            s.end_time_in_seconds - s.start_time_in_seconds
            for s in speaker_segments
        )
        first_appearance = min(s.start_time_in_seconds for s in speaker_segments)
        
        speaker_statistics.append(SpeakerInfo(
            name=speaker,
            segment_count=len(speaker_segments),
            total_speak_time_seconds=total_time,
            first_appearance_seconds=first_appearance
        ))
    
    # Sort by first appearance
    speaker_statistics.sort(key=lambda x: x.first_appearance_seconds)
    
    return available_speakers, speaker_statistics


def rebuild_transcript(segments: List[SpeakerSegment]) -> Dict[str, Any]:
    """
    Rebuild transcript data including full text and statistics.
    
    Args:
        segments: List of SpeakerSegment objects
        
    Returns:
        Dictionary with fullTranscript, availableSpeakers, and speakerStatistics
    """
    full_transcript = build_full_transcript(segments)
    available_speakers, speaker_statistics = calculate_speaker_statistics(segments)
    
    return {
        'fullTranscript': full_transcript,
        'availableSpeakers': available_speakers,
        'speakerStatistics': [s.to_dict() for s in speaker_statistics]
    }


def validate_segment_update(
    segment_index: Optional[int],
    new_text: str,
    old_text: str,
    new_speaker: Optional[str],
    old_speaker: str,
    segments_count: int
) -> Tuple[bool, bool, Optional[str]]:
    """
    Validate segment update parameters.
    
    Args:
        segment_index: Index of segment to update
        new_text: New text value
        old_text: Old text value
        new_speaker: New speaker value (optional)
        old_speaker: Old speaker value
        segments_count: Total number of segments
        
    Returns:
        Tuple of (text_changed, speaker_changed, error_message)
        If error_message is not None, validation failed
    """
    # Validate segment index
    if segment_index is None or segment_index < 0 or segment_index >= segments_count:
        return False, False, 'Invalid segment index'
    
    # Check what changed
    text_changed = new_text != old_text
    speaker_changed = new_speaker is not None and new_speaker != old_speaker
    
    if not text_changed and not speaker_changed:
        return False, False, 'No changes detected'
    
    return text_changed, speaker_changed, None


def create_audit_entry(
    segment_data: Dict[str, Any],
    segment_index: int,
    old_text: str,
    new_text: str,
    old_speaker: Optional[str] = None,
    new_speaker: Optional[str] = None,
    text_changed: bool = False,
    speaker_changed: bool = False
) -> Dict[str, Any]:
    """
    Create an audit log entry for a segment change.
    
    Args:
        segment_data: Current segment data
        segment_index: Index of the segment
        old_text: Original text
        new_text: New text
        old_speaker: Original speaker (optional)
        new_speaker: New speaker (optional)
        text_changed: Whether text was modified
        speaker_changed: Whether speaker was modified
        
    Returns:
        Audit log entry dictionary
    """
    # Determine action type
    if speaker_changed and text_changed:
        action = 'edit_with_speaker_change'
    elif speaker_changed:
        action = 'speaker_change'
    else:
        action = 'edit'
    
    # Create base entry
    audit_entry = {
        'timestamp': datetime.now().isoformat(),
        'action': action,
        'segmentIndex': segment_index,
        'lineNumber': segment_data.get('lineNumber'),
        'speaker': segment_data.get('speaker'),
        'oldText': old_text,
        'newText': new_text,
        'startTime': segment_data.get('uiFormattedStartTime', '0:00')
    }
    
    # Add speaker change info if applicable
    if speaker_changed and old_speaker is not None and new_speaker is not None:
        audit_entry['oldSpeaker'] = old_speaker
        audit_entry['newSpeaker'] = new_speaker
    
    return audit_entry


def build_segment_update_message(
    line_number: int,
    speaker_changed: bool,
    text_changed: bool,
    new_speaker: Optional[str] = None
) -> str:
    """
    Build a user-friendly message describing the segment update.
    
    Args:
        line_number: Segment line number
        speaker_changed: Whether speaker was changed
        text_changed: Whether text was changed
        new_speaker: New speaker name (if speaker was changed)
        
    Returns:
        Formatted message string
    """
    if speaker_changed and text_changed:
        return f'Segment #{line_number}: Speaker changed to "{new_speaker}" and text updated'
    elif speaker_changed:
        return f'Segment #{line_number}: Speaker changed to "{new_speaker}"'
    else:
        return f'Segment #{line_number}: Text updated'


def validate_json_request(data: Optional[Dict[str, Any]], required_fields: List[str]) -> Optional[str]:
    """
    Validate that a JSON request contains required fields.
    
    Args:
        data: Request JSON data
        required_fields: List of required field names
        
    Returns:
        Error message if validation fails, None if successful
    """
    if not data:
        return 'No data provided'
    
    missing_fields = [field for field in required_fields if field not in data]
    if missing_fields:
        return f'Missing required fields: {", ".join(missing_fields)}'
    
    return None


def validate_batch_transcription_params(
    enable_diarization_str: str,
    min_speakers_str: str,
    max_speakers_str: str
) -> Tuple[bool, int, int, Optional[str]]:
    """
    Validate and parse batch transcription parameters.
    
    Args:
        enable_diarization_str: String representation of boolean
        min_speakers_str: String representation of minimum speakers
        max_speakers_str: String representation of maximum speakers
        
    Returns:
        Tuple of (enable_diarization, min_speakers, max_speakers, error_message)
        If error_message is not None, validation failed
    """
    try:
        enable_diarization = enable_diarization_str.lower() == 'true'
        min_speakers = int(min_speakers_str)
        max_speakers = int(max_speakers_str)
        
        # Validate ranges
        if min_speakers < 1:
            return False, 0, 0, 'Minimum speakers must be at least 1'
        
        if max_speakers < min_speakers:
            return False, 0, 0, 'Maximum speakers must be greater than or equal to minimum speakers'
        
        if max_speakers > 20:  # Reasonable upper limit
            return False, 0, 0, 'Maximum speakers cannot exceed 20'
        
        return enable_diarization, min_speakers, max_speakers, None
        
    except ValueError as ex:
        return False, 0, 0, f'Invalid parameter format: {str(ex)}'


def generate_filename(prefix: str, suffix: str) -> str:
    """
    Generate a unique filename with timestamp.
    
    Args:
        prefix: Filename prefix (e.g., 'transcription_')
        suffix: File extension (e.g., '.json')
        
    Returns:
        Formatted filename with timestamp
    """
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    return f"{prefix}{timestamp}{suffix}"
