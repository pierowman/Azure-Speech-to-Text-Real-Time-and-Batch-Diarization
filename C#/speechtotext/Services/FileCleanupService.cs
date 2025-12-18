namespace speechtotext.Services
{
    public class FileCleanupService : IFileCleanupService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileCleanupService> _logger;

        public FileCleanupService(IWebHostEnvironment environment, ILogger<FileCleanupService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task CleanupOldFilesAsync(TimeSpan olderThan)
        {
            await Task.Run(() =>
            {
                try
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                    
                    if (!Directory.Exists(uploadsFolder))
                    {
                        return;
                    }

                    var files = Directory.GetFiles(uploadsFolder);
                    var cutoffDate = DateTime.UtcNow - olderThan;
                    var deletedCount = 0;

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < cutoffDate)
                        {
                            try
                            {
                                File.Delete(file);
                                deletedCount++;
                                _logger.LogInformation($"Deleted old file: {file}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"Failed to delete file: {file}");
                            }
                        }
                    }

                    _logger.LogInformation($"Cleanup completed. Deleted {deletedCount} file(s) older than {olderThan.TotalHours} hours.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during file cleanup");
                }
            });
        }

        public async Task DeleteFileAsync(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        _logger.LogInformation($"Deleted file: {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete file: {filePath}");
                }
            });
        }
    }
}
