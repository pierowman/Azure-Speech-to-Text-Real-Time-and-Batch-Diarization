namespace speechtotext.Models
{
    public class DownloadRequest
    {
        public string? JsonData { get; set; }
        public string? FullTranscript { get; set; }
        public List<SpeakerSegment>? Segments { get; set; }
        public string? GoldenRecordJsonData { get; set; }
        public List<AuditLogEntry>? AuditLog { get; set; }
    }
}
