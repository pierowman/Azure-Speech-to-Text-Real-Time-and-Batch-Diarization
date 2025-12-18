namespace speechtotext.Models
{
    public class UpdateSpeakersRequest
    {
        public List<SpeakerSegment> Segments { get; set; } = new();
        public string? AudioFileUrl { get; set; }
        public string? GoldenRecordJsonData { get; set; }
        public List<AuditLogEntry> AuditLog { get; set; } = new();
    }
}
