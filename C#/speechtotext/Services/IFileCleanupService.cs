namespace speechtotext.Services
{
    public interface IFileCleanupService
    {
        Task CleanupOldFilesAsync(TimeSpan olderThan);
        Task DeleteFileAsync(string filePath);
    }
}
