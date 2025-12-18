using speechtotext.Models;

namespace speechtotext.Services
{
    /// <summary>
    /// Service interface for Azure Speech Service batch transcription
    /// </summary>
    public interface IBatchTranscriptionService
    {
        /// <summary>
        /// Creates a batch transcription job in Azure Speech Service
        /// </summary>
        Task<TranscriptionJob> CreateBatchTranscriptionAsync(
            IEnumerable<string> audioFilePaths,
            string jobName,
            string language = "en-US",
            bool enableDiarization = true,
            int? minSpeakers = null,
            int? maxSpeakers = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the list of supported locales for batch transcription from Azure Speech Service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of supported locale codes (e.g., "en-US", "es-ES")</returns>
        Task<IEnumerable<string>> GetSupportedLocalesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed locale information including display names from Azure Speech Service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of locale information with codes and display names</returns>
        Task<IEnumerable<LocaleInfo>> GetSupportedLocalesWithNamesAsync(CancellationToken cancellationToken = default);
    }
}
