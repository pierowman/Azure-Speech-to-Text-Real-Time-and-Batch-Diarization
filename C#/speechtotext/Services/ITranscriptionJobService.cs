using speechtotext.Models;

namespace speechtotext.Services
{
    /// <summary>
    /// Service interface for managing Azure Speech Service batch transcription jobs
    /// </summary>
    public interface ITranscriptionJobService
    {
        /// <summary>
        /// Gets all transcription jobs from Azure Speech Service
        /// </summary>
        Task<List<TranscriptionJob>> GetTranscriptionJobsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific transcription job by ID
        /// </summary>
        Task<TranscriptionJob?> GetTranscriptionJobAsync(string jobId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a transcription job
        /// </summary>
        Task<bool> CancelTranscriptionJobAsync(string jobId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a transcription job
        /// </summary>
        Task<bool> DeleteTranscriptionJobAsync(string jobId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the transcription results for a completed job
        /// </summary>
        Task<BatchTranscriptionResult?> GetTranscriptionResultsAsync(string jobId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the transcription results for a specific file within a completed batch job
        /// </summary>
        Task<BatchTranscriptionResult?> GetTranscriptionResultsByFileAsync(string jobId, int fileIndex, CancellationToken cancellationToken = default);
    }
}
