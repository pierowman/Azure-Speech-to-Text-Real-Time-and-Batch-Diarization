namespace speechtotext.Models
{
    /// <summary>
    /// Represents a batch transcription job from Azure Speech Service
    /// </summary>
    public class TranscriptionJob
    {
        private const long TicksPerSecond = 10_000_000; // .NET TimeSpan tick definition

        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDateTime { get; set; }
        public DateTime? LastActionDateTime { get; set; }
        public string? Error { get; set; }
        public List<string> Files { get; set; } = new();
        public string? ResultsUrl { get; set; }
        public TranscriptionProperties? Properties { get; set; }
        
        /// <summary>
        /// Locale/Language code for the transcription (e.g., "en-US", "es-ES")
        /// </summary>
        public string? Locale { get; set; }
        
        /// <summary>
        /// Formatted duration for display (HH:MM:SS)
        /// </summary>
        public string FormattedDuration
        {
            get
            {
                var duration = Properties?.Duration;
                if (!duration.HasValue || duration.Value == 0)
                    return "N/A";
                
                // Convert ticks to seconds
                double seconds = duration.Value / (double)TicksPerSecond;
                var timeSpan = TimeSpan.FromSeconds(seconds);
                
                // Format as HH:MM:SS
                return timeSpan.ToString(@"hh\:mm\:ss");
            }
        }
        
        /// <summary>
        /// Total number of files in the batch
        /// </summary>
        public int TotalFileCount => Files?.Count ?? 0;
    }

    /// <summary>
    /// Additional properties for a transcription job
    /// </summary>
    public class TranscriptionProperties
    {
        /// <summary>
        /// Duration in ticks (100-nanosecond intervals)
        /// Note: This can be a very large number, so we use long instead of int
        /// </summary>
        public long? Duration { get; set; }
        
        public int? SucceededCount { get; set; }
        public int? FailedCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request model for canceling a transcription job
    /// </summary>
    public class CancelJobRequest
    {
        public string JobId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for job operations
    /// </summary>
    public class JobOperationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TranscriptionJob? Job { get; set; }
    }
}
