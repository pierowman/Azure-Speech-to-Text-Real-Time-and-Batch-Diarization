namespace speechtotext.Models
{
    /// <summary>
    /// Request model for batch transcription submission
    /// </summary>
    public class BatchTranscriptionRequest
    {
        /// <summary>
        /// Optional job name for easy identification
        /// </summary>
        public string? JobName { get; set; }
        
        /// <summary>
        /// Language code (e.g., "en-US")
        /// </summary>
        public string Language { get; set; } = "en-US";
        
        /// <summary>
        /// Enable speaker diarization
        /// </summary>
        public bool EnableDiarization { get; set; } = true;
    }
    
    /// <summary>
    /// Response model for batch transcription submission
    /// </summary>
    public class BatchTranscriptionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? JobId { get; set; }
        public string? JobName { get; set; }
        public int FilesSubmitted { get; set; }
    }
}
