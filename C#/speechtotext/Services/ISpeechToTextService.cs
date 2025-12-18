using speechtotext.Models;

namespace speechtotext.Services
{
    public interface ISpeechToTextService
    {
        Task<TranscriptionResult> TranscribeWithDiarizationAsync(
            string audioFilePath, 
            string? locale = null,
            CancellationToken cancellationToken = default);
    }
}
