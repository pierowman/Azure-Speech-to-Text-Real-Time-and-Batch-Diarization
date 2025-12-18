using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using speechtotext.Services;
using Xunit;

namespace speechtotext.Tests.Services;

/// <summary>
/// Comprehensive tests for FileCleanupService.
/// Tests file cleanup logic, error handling, and edge cases.
/// </summary>
public class FileCleanupServiceTests : IDisposable
{
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<ILogger<FileCleanupService>> _mockLogger;
    private readonly FileCleanupService _service;
    private readonly string _testDirectory;

    public FileCleanupServiceTests()
    {
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockLogger = new Mock<ILogger<FileCleanupService>>();

        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FileCleanupTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _mockEnvironment.Setup(x => x.WebRootPath).Returns(_testDirectory);
        _service = new FileCleanupService(_mockEnvironment.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task CleanupOldFilesAsync_ShouldDeleteOldFiles_WhenFilesExist()
    {
        // Arrange
        var uploadsFolder = Path.Combine(_testDirectory, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        // Create old file
        var oldFile = Path.Combine(uploadsFolder, "old.wav");
        File.WriteAllText(oldFile, "old content");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddHours(-2));

        // Create new file
        var newFile = Path.Combine(uploadsFolder, "new.wav");
        File.WriteAllText(newFile, "new content");
        File.SetLastWriteTimeUtc(newFile, DateTime.UtcNow);

        // Act
        await _service.CleanupOldFilesAsync(TimeSpan.FromHours(1));

        // Assert
        File.Exists(oldFile).Should().BeFalse("old file should be deleted");
        File.Exists(newFile).Should().BeTrue("new file should remain");

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted old file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupOldFilesAsync_ShouldNotDeleteNewFiles()
    {
        // Arrange
        var uploadsFolder = Path.Combine(_testDirectory, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var file1 = Path.Combine(uploadsFolder, "file1.wav");
        var file2 = Path.Combine(uploadsFolder, "file2.mp3");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, "content2");

        // Set files as recent
        File.SetLastWriteTimeUtc(file1, DateTime.UtcNow.AddMinutes(-30));
        File.SetLastWriteTimeUtc(file2, DateTime.UtcNow.AddMinutes(-45));

        // Act
        await _service.CleanupOldFilesAsync(TimeSpan.FromHours(1));

        // Assert
        File.Exists(file1).Should().BeTrue();
        File.Exists(file2).Should().BeTrue();

        // Verify completion logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleanup completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupOldFilesAsync_ShouldHandleNonExistentDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid()}");
        _mockEnvironment.Setup(x => x.WebRootPath).Returns(tempDir);
        var service = new FileCleanupService(_mockEnvironment.Object, _mockLogger.Object);

        // Act & Assert - Should not throw
        await service.CleanupOldFilesAsync(TimeSpan.FromHours(1));

        // Verify no error logging occurred (directory doesn't exist is not an error)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupOldFilesAsync_ShouldDeleteMultipleOldFiles()
    {
        // Arrange
        var uploadsFolder = Path.Combine(_testDirectory, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var files = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var file = Path.Combine(uploadsFolder, $"old{i}.wav");
            File.WriteAllText(file, $"content{i}");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-3));
            files.Add(file);
        }

        // Act
        await _service.CleanupOldFilesAsync(TimeSpan.FromHours(1));

        // Assert
        files.Should().AllSatisfy(f => File.Exists(f).Should().BeFalse());

        // Verify 5 files were deleted
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted old file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task CleanupOldFilesAsync_ShouldLogCompletionWithCorrectCount()
    {
        // Arrange
        var uploadsFolder = Path.Combine(_testDirectory, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        for (int i = 0; i < 3; i++)
        {
            var file = Path.Combine(uploadsFolder, $"file{i}.wav");
            File.WriteAllText(file, "content");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-2));
        }

        // Act
        await _service.CleanupOldFilesAsync(TimeSpan.FromHours(1));

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Deleted 3 file(s)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupOldFilesAsync_ShouldHandleFileDeleteException()
    {
        // Arrange
        var uploadsFolder = Path.Combine(_testDirectory, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var file = Path.Combine(uploadsFolder, "locked.wav");
        File.WriteAllText(file, "content");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-2));

        // Lock the file
        using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        await _service.CleanupOldFilesAsync(TimeSpan.FromHours(1));

        // Assert - File should still exist because it was locked
        File.Exists(file).Should().BeTrue();

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to delete file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldDeleteFile_WhenFileExists()
    {
        // Arrange
        var file = Path.Combine(_testDirectory, "todelete.wav");
        File.WriteAllText(file, "content");

        // Act
        await _service.DeleteFileAsync(file);

        // Assert
        File.Exists(file).Should().BeFalse();

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldNotThrow_WhenFileDoesNotExist()
    {
        // Arrange
        var file = Path.Combine(_testDirectory, "nonexistent.wav");

        // Act & Assert - Should not throw
        await _service.DeleteFileAsync(file);

        // Verify no logging occurred
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldLogWarning_WhenDeletionFails()
    {
        // Arrange
        var file = Path.Combine(_testDirectory, "locked.wav");
        File.WriteAllText(file, "content");

        // Lock the file
        using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        await _service.DeleteFileAsync(file);

        // Assert
        File.Exists(file).Should().BeTrue();

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to delete file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupOldFilesAsync_ShouldHandleEmptyDirectory()
    {
        // Arrange
        var uploadsFolder = Path.Combine(_testDirectory, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        // Act
        await _service.CleanupOldFilesAsync(TimeSpan.FromHours(1));

        // Assert - Should complete successfully with 0 files deleted
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Deleted 0 file(s)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(168)] // 1 week
    public async Task CleanupOldFilesAsync_ShouldRespectTimeSpanParameter(int hours)
    {
        // Arrange
        var uploadsFolder = Path.Combine(_testDirectory, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var file = Path.Combine(uploadsFolder, "file.wav");
        File.WriteAllText(file, "content");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-(hours + 1)));

        // Act
        await _service.CleanupOldFilesAsync(TimeSpan.FromHours(hours));

        // Assert
        File.Exists(file).Should().BeFalse();

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains($"{hours} hours")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupOldFilesAsync_ShouldNotDeleteFileAtExactCutoff()
    {
        // Arrange
        var uploadsFolder = Path.Combine(_testDirectory, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var file = Path.Combine(uploadsFolder, "exact.wav");
        File.WriteAllText(file, "content");
        
        // Set file slightly newer than cutoff to ensure it's not deleted
        var cutoffTime = DateTime.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(file, cutoffTime.AddSeconds(1));

        // Act
        await _service.CleanupOldFilesAsync(TimeSpan.FromHours(1));

        // Assert - File newer than cutoff should NOT be deleted
        File.Exists(file).Should().BeTrue();
    }
}
