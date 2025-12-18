namespace speechtotext.Models
{
    /// <summary>
    /// Defines the transcription processing mode
    /// </summary>
    public enum TranscriptionMode
    {
        /// <summary>
        /// Real-time transcription for immediate results (single file, smaller size)
        /// </summary>
        RealTime = 0,
        
        /// <summary>
        /// Batch transcription for large files or multiple files (async processing)
        /// </summary>
        Batch = 1
    }
}
