namespace speechtotext.Models
{
    public class TranscriptionResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<SpeakerSegment> Segments { get; set; } = new();
        public string? FullTranscript { get; set; }
        public string? AudioFileUrl { get; set; }
        public string? RawJsonData { get; set; }
        public string? GoldenRecordJsonData { get; set; } // Stores the original unmodified Azure Speech Service result
        
        // Available speakers list (calculated on server)
        public List<string> AvailableSpeakers { get; set; } = new();
        
        // Speaker statistics (calculated on server)
        public List<SpeakerInfo> SpeakerStatistics { get; set; } = new();
        
        // Audit log tracking all edits
        public List<AuditLogEntry> AuditLog { get; set; } = new();
    }
    
    /// <summary>
    /// Represents the transcription results from a batch job
    /// </summary>
    public class BatchTranscriptionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<SpeakerSegment> Segments { get; set; } = new();
        public string FullTranscript { get; set; } = string.Empty;
        public List<string> AvailableSpeakers { get; set; } = new();
        public List<SpeakerInfo> SpeakerStatistics { get; set; } = new();
        public string RawJsonData { get; set; } = string.Empty;
        
        // New properties for per-file support
        public List<FileTranscriptionInfo> FileResults { get; set; } = new();
        public int TotalFiles { get; set; }
    }
    
    /// <summary>
    /// Represents transcription information for an individual file within a batch job
    /// </summary>
    public class FileTranscriptionInfo
    {
        public string FileName { get; set; } = string.Empty;
        public int Channel { get; set; }
        public List<SpeakerSegment> Segments { get; set; } = new();
        public string FullTranscript { get; set; } = string.Empty;
        public List<string> AvailableSpeakers { get; set; } = new();
        public List<SpeakerInfo> SpeakerStatistics { get; set; } = new();
        public long DurationInTicks { get; set; }
        public double DurationInSeconds => DurationInTicks / 10000000.0;
        public string FormattedDuration => TimeSpan.FromSeconds(DurationInSeconds).ToString(@"hh\:mm\:ss");
    }

    public class SpeakerSegment
    {
        public string Speaker { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public long OffsetInTicks { get; set; }
        public long DurationInTicks { get; set; }
        
        // Line number for display (calculated on server)
        public int LineNumber { get; set; }
        
        // Azure Speech SDK uses 100-nanosecond units (ticks), not .NET ticks
        // Convert to seconds for audio playback synchronization
        public double StartTimeInSeconds => OffsetInTicks / 10000000.0;
        public double EndTimeInSeconds => (OffsetInTicks + DurationInTicks) / 10000000.0;
        
        // UI-compatible timestamp format (matches JavaScript: new Date(segment.startTimeInSeconds * 1000).toISOString().substr(11, 8))
        // This is the format displayed in the UI and used in downloads
        public string UIFormattedStartTime => TimeSpan.FromSeconds(StartTimeInSeconds).ToString(@"hh\:mm\:ss");
        
        // Legacy properties kept for backward compatibility
        public string FormattedStartTime => TimeSpan.FromTicks(OffsetInTicks / 10).ToString(@"hh\:mm\:ss");
        public string FormattedDuration => TimeSpan.FromTicks(DurationInTicks / 10).ToString(@"hh\:mm\:ss");
        
        public string? OriginalSpeaker { get; set; } // Stores the original speaker name from Azure
        public string? OriginalText { get; set; } // Stores the original transcribed text from Azure
        
        // Change detection indicators (calculated properties)
        public bool SpeakerWasChanged => !string.IsNullOrEmpty(OriginalSpeaker) && Speaker != OriginalSpeaker;
        public bool TextWasChanged => !string.IsNullOrEmpty(OriginalText) && Text != OriginalText;
    }

    public class SpeakerInfo
    {
        public string Name { get; set; } = string.Empty;
        public int SegmentCount { get; set; }
        public double TotalSpeakTimeSeconds { get; set; }
        public double FirstAppearanceSeconds { get; set; }
        
        // Formatted properties for display
        public string TotalSpeakTimeFormatted => TimeSpan.FromSeconds(TotalSpeakTimeSeconds).ToString(@"hh\:mm\:ss");
        public string FirstAppearanceFormatted => TimeSpan.FromSeconds(FirstAppearanceSeconds).ToString(@"hh\:mm\:ss");
    }

    public class AuditLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string ChangeType { get; set; } = string.Empty; // "SpeakerEdit", "TextEdit", "SpeakerReassignment"
        public int? LineNumber { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? AdditionalInfo { get; set; } // For reassignments: "5 segments reassigned"
    }
}
