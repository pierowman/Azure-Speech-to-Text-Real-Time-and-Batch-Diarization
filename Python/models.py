"""
Data models for transcription results and related data structures
"""
from dataclasses import dataclass, field
from typing import List, Optional
from datetime import datetime


@dataclass
class SpeakerSegment:
    """Represents a single speech segment from a speaker"""
    speaker: str = ""
    text: str = ""
    offset_in_ticks: int = 0
    duration_in_ticks: int = 0
    line_number: int = 0
    original_speaker: Optional[str] = None
    original_text: Optional[str] = None
    
    @property
    def start_time_in_seconds(self) -> float:
        """Convert ticks to seconds (Azure uses 100-nanosecond units)"""
        return self.offset_in_ticks / 10_000_000.0
    
    @property
    def end_time_in_seconds(self) -> float:
        """Calculate end time in seconds"""
        return (self.offset_in_ticks + self.duration_in_ticks) / 10_000_000.0
    
    @property
    def ui_formatted_start_time(self) -> str:
        """Format start time as HH:MM:SS for UI"""
        hours = int(self.start_time_in_seconds // 3600)
        minutes = int((self.start_time_in_seconds % 3600) // 60)
        seconds = int(self.start_time_in_seconds % 60)
        return f"{hours:02d}:{minutes:02d}:{seconds:02d}"
    
    @property
    def speaker_was_changed(self) -> bool:
        """Check if speaker name was modified"""
        return bool(self.original_speaker) and self.speaker != self.original_speaker
    
    @property
    def text_was_changed(self) -> bool:
        """Check if text was modified"""
        return bool(self.original_text) and self.text != self.original_text
    
    def to_dict(self):
        """Convert to dictionary for JSON serialization"""
        return {
            'speaker': self.speaker,
            'text': self.text,
            'offsetInTicks': self.offset_in_ticks,
            'durationInTicks': self.duration_in_ticks,
            'lineNumber': self.line_number,
            'originalSpeaker': self.original_speaker,
            'originalText': self.original_text,
            'startTimeInSeconds': self.start_time_in_seconds,
            'endTimeInSeconds': self.end_time_in_seconds,
            'uiFormattedStartTime': self.ui_formatted_start_time,
            'speakerWasChanged': self.speaker_was_changed,
            'textWasChanged': self.text_was_changed
        }


@dataclass
class SpeakerInfo:
    """Statistics about a speaker"""
    name: str = ""
    segment_count: int = 0
    total_speak_time_seconds: float = 0.0
    first_appearance_seconds: float = 0.0
    
    @property
    def total_speak_time_formatted(self) -> str:
        """Format total speak time as HH:MM:SS"""
        hours = int(self.total_speak_time_seconds // 3600)
        minutes = int((self.total_speak_time_seconds % 3600) // 60)
        seconds = int(self.total_speak_time_seconds % 60)
        return f"{hours:02d}:{minutes:02d}:{seconds:02d}"
    
    @property
    def first_appearance_formatted(self) -> str:
        """Format first appearance as HH:MM:SS"""
        hours = int(self.first_appearance_seconds // 3600)
        minutes = int((self.first_appearance_seconds % 3600) // 60)
        seconds = int(self.first_appearance_seconds % 60)
        return f"{hours:02d}:{minutes:02d}:{seconds:02d}"
    
    def to_dict(self):
        """Convert to dictionary for JSON serialization"""
        return {
            'name': self.name,
            'segmentCount': self.segment_count,
            'totalSpeakTimeSeconds': self.total_speak_time_seconds,
            'firstAppearanceSeconds': self.first_appearance_seconds,
            'totalSpeakTimeFormatted': self.total_speak_time_formatted,
            'firstAppearanceFormatted': self.first_appearance_formatted
        }


@dataclass
class AuditLogEntry:
    """Represents a single edit in the audit log"""
    timestamp: datetime = field(default_factory=datetime.utcnow)
    change_type: str = ""  # "SpeakerEdit", "TextEdit", "SpeakerReassignment"
    line_number: Optional[int] = None
    old_value: Optional[str] = None
    new_value: Optional[str] = None
    additional_info: Optional[str] = None
    
    def to_dict(self):
        """Convert to dictionary for JSON serialization"""
        return {
            'timestamp': self.timestamp.isoformat(),
            'changeType': self.change_type,
            'lineNumber': self.line_number,
            'oldValue': self.old_value,
            'newValue': self.new_value,
            'additionalInfo': self.additional_info
        }


@dataclass
class TranscriptionResult:
    """Complete transcription result"""
    success: bool = False
    message: str = ""
    segments: List[SpeakerSegment] = field(default_factory=list)
    full_transcript: str = ""
    audio_file_url: Optional[str] = None
    raw_json_data: Optional[str] = None
    golden_record_json_data: Optional[str] = None
    available_speakers: List[str] = field(default_factory=list)
    speaker_statistics: List[SpeakerInfo] = field(default_factory=list)
    audit_log: List[AuditLogEntry] = field(default_factory=list)
    
    def to_dict(self):
        """Convert to dictionary for JSON serialization"""
        return {
            'success': self.success,
            'message': self.message,
            'segments': [s.to_dict() for s in self.segments],
            'fullTranscript': self.full_transcript,
            'audioFileUrl': self.audio_file_url,
            'rawJsonData': self.raw_json_data,
            'goldenRecordJsonData': self.golden_record_json_data,
            'availableSpeakers': self.available_speakers,
            'speakerStatistics': [s.to_dict() for s in self.speaker_statistics],
            'auditLog': [a.to_dict() for a in self.audit_log]
        }


@dataclass
class LocaleInfo:
    """Language locale information"""
    code: str = ""
    name: str = ""
    
    def to_dict(self):
        """Convert to dictionary for JSON serialization"""
        return {
            'code': self.code,
            'name': self.name
        }


@dataclass
class TranscriptionProperties:
    """Additional properties for a transcription job"""
    duration: Optional[int] = None  # in ticks
    succeeded_count: Optional[int] = None
    failed_count: Optional[int] = None
    error_message: Optional[str] = None
    
    def to_dict(self):
        """Convert to dictionary for JSON serialization"""
        return {
            'duration': self.duration,
            'succeededCount': self.succeeded_count,
            'failedCount': self.failed_count,
            'errorMessage': self.error_message
        }


@dataclass
class TranscriptionJob:
    """Represents a batch transcription job"""
    id: str = ""
    display_name: str = ""
    status: str = ""
    created_date_time: Optional[datetime] = None
    last_action_date_time: Optional[datetime] = None
    error: Optional[str] = None
    files: List[str] = field(default_factory=list)
    results_url: Optional[str] = None
    properties: Optional[TranscriptionProperties] = None
    locale: Optional[str] = None
    
    @property
    def formatted_duration(self) -> str:
        """Format duration as HH:MM:SS"""
        if not self.properties or not self.properties.duration:
            return "N/A"
        
        seconds = self.properties.duration / 10_000_000.0
        hours = int(seconds // 3600)
        minutes = int((seconds % 3600) // 60)
        secs = int(seconds % 60)
        return f"{hours:02d}:{minutes:02d}:{secs:02d}"
    
    @property
    def total_file_count(self) -> int:
        """Total number of files in the batch"""
        return len(self.files)
    
    def to_dict(self):
        """Convert to dictionary for JSON serialization"""
        return {
            'id': self.id,
            'displayName': self.display_name,
            'status': self.status,
            'createdDateTime': self.created_date_time.isoformat() if self.created_date_time else None,
            'lastActionDateTime': self.last_action_date_time.isoformat() if self.last_action_date_time else None,
            'error': self.error,
            'files': self.files,
            'resultsUrl': self.results_url,
            'properties': self.properties.to_dict() if self.properties else None,
            'locale': self.locale,
            'formattedDuration': self.formatted_duration,
            'totalFileCount': self.total_file_count
        }


@dataclass
class BatchTranscriptionResult:
    """Results from a batch transcription job"""
    success: bool = False
    message: str = ""
    job_id: str = ""
    display_name: str = ""
    segments: List[SpeakerSegment] = field(default_factory=list)
    full_transcript: str = ""
    available_speakers: List[str] = field(default_factory=list)
    speaker_statistics: List[SpeakerInfo] = field(default_factory=list)
    raw_json_data: str = ""
    
    def to_dict(self):
        """Convert to dictionary for JSON serialization"""
        return {
            'success': self.success,
            'message': self.message,
            'jobId': self.job_id,
            'displayName': self.display_name,
            'segments': [s.to_dict() for s in self.segments],
            'fullTranscript': self.full_transcript,
            'availableSpeakers': self.available_speakers,
            'speakerStatistics': [s.to_dict() for s in self.speaker_statistics],
            'rawJsonData': self.raw_json_data
        }
