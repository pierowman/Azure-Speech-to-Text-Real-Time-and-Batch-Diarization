"""
Utility functions and helpers
"""
import uuid
from typing import List
from models import SpeakerSegment, SpeakerInfo


def generate_request_id() -> str:
    """Generate a unique request ID for tracking"""
    return str(uuid.uuid4())


def rebuild_transcript(segments: List[SpeakerSegment]) -> dict:
    """
    Rebuild full transcript and calculate statistics from segments
    
    Args:
        segments: List of speaker segments
        
    Returns:
        Dictionary containing full transcript, available speakers, and statistics
    """
    # Build full transcript
    transcript_lines = [f"[{s.speaker}]: {s.text}" for s in segments]
    full_transcript = "\n".join(transcript_lines)
    
    # Calculate available speakers
    available_speakers = sorted(list(set(s.speaker for s in segments if s.speaker.strip())))
    
    # Calculate speaker statistics
    speaker_statistics = calculate_speaker_statistics(segments)
    
    return {
        'fullTranscript': full_transcript,
        'availableSpeakers': available_speakers,
        'speakerStatistics': [s.to_dict() for s in speaker_statistics]
    }


def calculate_speaker_statistics(segments: List[SpeakerSegment]) -> List[SpeakerInfo]:
    """
    Calculate statistics for each speaker
    
    Args:
        segments: List of speaker segments
        
    Returns:
        List of SpeakerInfo objects sorted by first appearance
    """
    speaker_groups = {}
    for segment in segments:
        if segment.speaker not in speaker_groups:
            speaker_groups[segment.speaker] = []
        speaker_groups[segment.speaker].append(segment)
    
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
    
    return speaker_statistics


def assign_line_numbers(segments: List[SpeakerSegment]) -> None:
    """
    Assign sequential line numbers to segments (in-place)
    
    Args:
        segments: List of speaker segments to number
    """
    for i, segment in enumerate(segments, 1):
        segment.line_number = i


def parse_segments_from_dict(segments_data: List[dict]) -> List[SpeakerSegment]:
    """
    Parse segments from dictionary representation
    
    Args:
        segments_data: List of segment dictionaries
        
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
