namespace speechtotext.Models
{
    public class AudioUploadOptions
    {
        // Real-Time Transcription Settings
        public string[] RealTimeAllowedExtensions { get; set; } = new[] { ".wav" };
        public long RealTimeMaxFileSizeInBytes { get; set; } = 25 * 1024 * 1024; // 25 MB
        public int RealTimeMaxDurationInMinutes { get; set; } = 60;
        
        // Batch Transcription Settings
        public string[] BatchAllowedExtensions { get; set; } = new[] { ".wav", ".mp3", ".flac", ".ogg" };
        public long BatchMaxFileSizeInBytes { get; set; } = 1024 * 1024 * 1024; // 1 GB
        public int BatchMaxDurationInMinutes { get; set; } = 480; // 8 hours
        public int BatchMaxFiles { get; set; } = 20;
        
        /// <summary>
        /// Auto-refresh interval in seconds for batch transcription jobs
        /// When jobs are running, the UI will automatically refresh at this interval
        /// Default: 60 seconds
        /// </summary>
        public int BatchJobAutoRefreshSeconds { get; set; } = 60;
        
        /// <summary>
        /// Default minimum number of speakers for batch transcription diarization
        /// Default: 2
        /// </summary>
        public int DefaultMinSpeakers { get; set; } = 2;
        
        /// <summary>
        /// Default maximum number of speakers for batch transcription diarization
        /// Default: 3
        /// </summary>
        public int DefaultMaxSpeakers { get; set; } = 3;
        
        /// <summary>
        /// Default locale/language for batch transcription
        /// Default: en-US (English - United States)
        /// Supported locales: en-US, en-GB, es-ES, es-MX, fr-FR, fr-CA, de-DE, it-IT, 
        /// pt-BR, pt-PT, zh-CN, zh-TW, ja-JP, ko-KR, ar-SA, hi-IN, ru-RU, nl-NL, 
        /// pl-PL, sv-SE, da-DK, no-NO, fi-FI, tr-TR, th-TH, vi-VN
        /// </summary>
        public string DefaultLocale { get; set; } = "en-US";
        
        /// <summary>
        /// Duration in hours to cache the supported locales list from Azure Speech Service.
        /// Since supported locales rarely change, this cache duration can be quite long.
        /// Set to 0 to disable caching (fetch on every request).
        /// Default: 24 hours (1 day)
        /// </summary>
        public int LocalesCacheDurationHours { get; set; } = 24;
        
        // Common Settings
        public string UploadFolderPath { get; set; } = "uploads";
        public bool ShowTranscriptionJobsTab { get; set; } = true;
        
        // Feature Toggles
        /// <summary>
        /// Enable or disable batch transcription mode in the UI
        /// When false, only real-time transcription mode is available
        /// Default: true
        /// </summary>
        public bool EnableBatchTranscription { get; set; } = true;
        
        // Backward compatibility properties - kept for existing code
        [Obsolete("Use RealTimeAllowedExtensions instead")]
        public string[] AllowedExtensions 
        { 
            get => RealTimeAllowedExtensions; 
            set => RealTimeAllowedExtensions = value; 
        }
        
        [Obsolete("Use RealTimeMaxFileSizeInBytes instead")]
        public long MaxFileSizeInBytes 
        { 
            get => RealTimeMaxFileSizeInBytes; 
            set => RealTimeMaxFileSizeInBytes = value; 
        }
        
        [Obsolete("Use RealTimeMaxDurationInMinutes instead")]
        public int MaxDurationInMinutes 
        { 
            get => RealTimeMaxDurationInMinutes; 
            set => RealTimeMaxDurationInMinutes = value; 
        }
    }
}
