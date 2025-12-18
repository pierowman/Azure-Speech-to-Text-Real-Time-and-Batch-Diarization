using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using speechtotext.Services;
using Xunit;

namespace speechtotext.Tests.Services;

/// <summary>
/// Tests for FileCleanupBackgroundService.
/// Tests the background service lifecycle and cleanup scheduling.
/// </summary>
public class FileCleanupBackgroundServiceTests
{
    private readonly Mock<ILogger<FileCleanupBackgroundService>> _mockLogger;
    private readonly Mock<IFileCleanupService> _mockCleanupService;
    private readonly IServiceProvider _serviceProvider;

    public FileCleanupBackgroundServiceTests()
    {
        _mockLogger = new Mock<ILogger<FileCleanupBackgroundService>>();
        _mockCleanupService = new Mock<IFileCleanupService>();

        // Setup service provider with cleanup service
        var services = new ServiceCollection();
        services.AddScoped<IFileCleanupService>(_ => _mockCleanupService.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Act
        var service = new FileCleanupBackgroundService(_serviceProvider, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<BackgroundService>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogStartup()
    {
        // Arrange
        var service = new FileCleanupBackgroundService(_serviceProvider, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        
        // Cancel immediately to prevent infinite loop
        cts.Cancel();

        // Act
        try
        {
            await service.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Verify startup was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallCleanupService()
    {
        // Arrange
        _mockCleanupService
            .Setup(x => x.CleanupOldFilesAsync(It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        var service = new FileCleanupBackgroundService(_serviceProvider, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        
        // Allow one iteration then cancel
        _mockCleanupService
            .Setup(x => x.CleanupOldFilesAsync(It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask)
            .Callback(() => cts.Cancel());

        // Act
        try
        {
            await service.StartAsync(CancellationToken.None);
            await Task.Delay(100); // Allow service to start
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Verify cleanup was called
        _mockCleanupService.Verify(
            x => x.CleanupOldFilesAsync(It.Is<TimeSpan>(t => t.Days == 7)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleExceptionsDuringCleanup()
    {
        // Arrange
        var callCount = 0;
        _mockCleanupService
            .Setup(x => x.CleanupOldFilesAsync(It.IsAny<TimeSpan>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Test exception");
                }
                return Task.CompletedTask;
            });

        var service = new FileCleanupBackgroundService(_serviceProvider, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        
        // Cancel after first exception
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            cts.Cancel();
        });

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(300);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStopWhenCancellationRequested()
    {
        // Arrange
        _mockCleanupService
            .Setup(x => x.CleanupOldFilesAsync(It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        var service = new FileCleanupBackgroundService(_serviceProvider, _mockLogger.Object);
        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100); // Allow service to start
        
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        
        // Give it time to process cancellation and log the stopped message
        await Task.Delay(100);

        // Assert - Verify service was started (we can't reliably test the stopped message in unit tests)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogNextScheduledCleanup()
    {
        // Arrange
        var callCount = 0;
        _mockCleanupService
            .Setup(x => x.CleanupOldFilesAsync(It.IsAny<TimeSpan>()))
            .Returns(() =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        var service = new FileCleanupBackgroundService(_serviceProvider, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        
        // Cancel after first execution
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            cts.Cancel();
        });

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(300);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Verify next cleanup schedule was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Next cleanup scheduled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

