namespace speechtotext.Services
{
    public class FileCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FileCleanupBackgroundService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24); // Run daily
        private readonly TimeSpan _fileRetentionPeriod = TimeSpan.FromDays(7); // Keep files for 7 days

        public FileCleanupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<FileCleanupBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("File Cleanup Background Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting scheduled file cleanup...");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var cleanupService = scope.ServiceProvider.GetRequiredService<IFileCleanupService>();
                        await cleanupService.CleanupOldFilesAsync(_fileRetentionPeriod);
                    }

                    _logger.LogInformation($"Next cleanup scheduled in {_cleanupInterval.TotalHours} hours.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during scheduled file cleanup.");
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }

            _logger.LogInformation("File Cleanup Background Service stopped.");
        }
    }
}
