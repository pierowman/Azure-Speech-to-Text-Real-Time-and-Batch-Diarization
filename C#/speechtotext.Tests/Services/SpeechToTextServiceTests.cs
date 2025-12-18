using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using speechtotext.Exceptions;
using speechtotext.Services;
using Xunit;

namespace speechtotext.Tests.Services;

public class SpeechToTextServiceTests
{
    private readonly Mock<ILogger<SpeechToTextService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public SpeechToTextServiceTests()
    {
        _mockLogger = new Mock<ILogger<SpeechToTextService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Setup default locale configuration
        _mockConfiguration.Setup(x => x["AudioUpload:DefaultLocale"]).Returns("en-US");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenSubscriptionKeyMissing()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns((string?)null);
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");

        // Act & Assert
        var act = () => new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*subscription key*");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenRegionMissing()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns((string?)null);

        // Act & Assert
        var act = () => new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*region*");
    }

    [Fact]
    public void Constructor_ShouldSucceed_WithValidConfiguration()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        _mockConfiguration.Setup(x => x["AzureSpeech:Endpoint"]).Returns((string?)null);

        // Act
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldAcceptCustomEndpoint()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        _mockConfiguration.Setup(x => x["AzureSpeech:Endpoint"]).Returns("https://custom.cognitiveservices.azure.com/");

        // Act
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldThrowArgumentException_WhenAudioFilePathIsNull()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Act & Assert
        var act = async () => await service.TranscribeWithDiarizationAsync(null!, null, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Audio file path*");
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldThrowArgumentException_WhenAudioFilePathIsEmpty()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Act & Assert
        var act = async () => await service.TranscribeWithDiarizationAsync("", null, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Audio file path*");
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldThrowArgumentException_WhenAudioFilePathIsWhitespace()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Act & Assert
        var act = async () => await service.TranscribeWithDiarizationAsync("   ", null, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Audio file path*");
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldThrowFileNotFoundException_WhenFileDoesNotExist()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        var nonExistentFile = "C:\\nonexistent\\file.wav";

        // Act & Assert
        var act = async () => await service.TranscribeWithDiarizationAsync(nonExistentFile, null, CancellationToken.None);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldThrowOperationCanceledException_WhenCancellationRequested()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        
        // Create a temp file
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "dummy content");

        try
        {
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            var act = async () => await service.TranscribeWithDiarizationAsync(tempFile, null, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldThrowTranscriptionException_OnInvalidWavFile()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        
        // Create a temp file with invalid WAV content
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "This is not a valid WAV file");

        try
        {
            // Act & Assert
            var act = async () => await service.TranscribeWithDiarizationAsync(tempFile, null, CancellationToken.None);
            await act.Should().ThrowAsync<TranscriptionException>();
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData("test-key-1", "westus")]
    [InlineData("test-key-2", "eastus2")]
    [InlineData("prod-key", "centralus")]
    public void Constructor_ShouldAcceptDifferentRegions(string subscriptionKey, string region)
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns(subscriptionKey);
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns(region);
        _mockConfiguration.Setup(x => x["AzureSpeech:Endpoint"]).Returns((string?)null);

        // Act
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldHandleEmptyEndpoint()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        _mockConfiguration.Setup(x => x["AzureSpeech:Endpoint"]).Returns(string.Empty);

        // Act
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldLogInformation_WhenStartingTranscription()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.wav");
        
        // Create a minimal WAV file header (44 bytes)
        var wavHeader = new byte[]
        {
            0x52, 0x49, 0x46, 0x46, // "RIFF"
            0x24, 0x00, 0x00, 0x00, // ChunkSize
            0x57, 0x41, 0x56, 0x45, // "WAVE"
            0x66, 0x6D, 0x74, 0x20, // "fmt "
            0x10, 0x00, 0x00, 0x00, // Subchunk1Size
            0x01, 0x00,             // AudioFormat (PCM)
            0x01, 0x00,             // NumChannels (mono)
            0x44, 0xAC, 0x00, 0x00, // SampleRate (44100)
            0x88, 0x58, 0x01, 0x00, // ByteRate
            0x02, 0x00,             // BlockAlign
            0x10, 0x00,             // BitsPerSample
            0x64, 0x61, 0x74, 0x61, // "data"
            0x00, 0x00, 0x00, 0x00  // Subchunk2Size
        };
        
        await File.WriteAllBytesAsync(tempFile, wavHeader);

        try
        {
            // Act
            try
            {
                await service.TranscribeWithDiarizationAsync(tempFile, null, CancellationToken.None);
            }
            catch
            {
                // Expected to fail with Azure SDK, but should have logged
            }

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting transcription for file")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Constructor_ShouldNotLog_DuringConstruction()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");

        // Act
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert - No logging should occur during construction
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
    public async Task TranscribeWithDiarizationAsync_ShouldHandleInvalidFilePath_WithSpecialCharacters()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        var invalidPath = "C:\\<invalid>\\path\\audio.wav";

        // Act & Assert
        var act = async () => await service.TranscribeWithDiarizationAsync(invalidPath, null, CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public void Constructor_ShouldUseDefaultLocale_WhenNotConfigured()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        _mockConfiguration.Setup(x => x["AudioUpload:DefaultLocale"]).Returns((string?)null);

        // Act
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void Constructor_ShouldAcceptDifferentDefaultLocales(string defaultLocale)
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        _mockConfiguration.Setup(x => x["AudioUpload:DefaultLocale"]).Returns(defaultLocale);

        // Act
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldLogSelectedLocale_WhenProvided()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        _mockConfiguration.Setup(x => x["AudioUpload:DefaultLocale"]).Returns("en-US");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.wav");
        var wavHeader = new byte[]
        {
            0x52, 0x49, 0x46, 0x46, // "RIFF"
            0x24, 0x00, 0x00, 0x00, // ChunkSize
            0x57, 0x41, 0x56, 0x45, // "WAVE"
            0x66, 0x6D, 0x74, 0x20, // "fmt "
            0x10, 0x00, 0x00, 0x00, // Subchunk1Size
            0x01, 0x00,             // AudioFormat (PCM)
            0x01, 0x00,             // NumChannels (mono)
            0x44, 0xAC, 0x00, 0x00, // SampleRate (44100)
            0x88, 0x58, 0x01, 0x00, // ByteRate
            0x02, 0x00,             // BlockAlign
            0x10, 0x00,             // BitsPerSample
            0x64, 0x61, 0x74, 0x61, // "data"
            0x00, 0x00, 0x00, 0x00  // Subchunk2Size
        };
        await File.WriteAllBytesAsync(tempFile, wavHeader);

        try
        {
            try
            {
                await service.TranscribeWithDiarizationAsync(tempFile, "es-ES", CancellationToken.None);
            }
            catch { }

            // Assert - Should log the provided locale
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("es-ES")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TranscribeWithDiarizationAsync_ShouldUseDefaultLocale_WhenNullProvided()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:SubscriptionKey"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");
        _mockConfiguration.Setup(x => x["AudioUpload:DefaultLocale"]).Returns("fr-FR");
        var service = new SpeechToTextService(_mockConfiguration.Object, _mockLogger.Object);
        
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.wav");
        var wavHeader = new byte[]
        {
            0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
            0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
            0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
            0x44, 0xAC, 0x00, 0x00, 0x88, 0x58, 0x01, 0x00,
            0x02, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61,
            0x00, 0x00, 0x00, 0x00
        };
        await File.WriteAllBytesAsync(tempFile, wavHeader);

        try
        {
            try
            {
                await service.TranscribeWithDiarizationAsync(tempFile, null, CancellationToken.None);
            }
            catch { }

            // Assert - Should log the default locale (fr-FR)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("fr-FR")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
