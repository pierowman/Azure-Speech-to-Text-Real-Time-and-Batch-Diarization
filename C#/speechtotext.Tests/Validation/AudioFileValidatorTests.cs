using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using speechtotext.Exceptions;
using speechtotext.Models;
using speechtotext.Validation;
using Xunit;

namespace speechtotext.Tests.Validation;

/// <summary>
/// Comprehensive tests for AudioFileValidator to ensure proper file validation.
/// Tests cover all validation scenarios: null files, empty files, size limits, and extensions.
/// </summary>
public class AudioFileValidatorTests
{
    private readonly Mock<ILogger<AudioFileValidator>> _mockLogger;
    private readonly AudioUploadOptions _options;
    private readonly AudioFileValidator _validator;

    public AudioFileValidatorTests()
    {
        _mockLogger = new Mock<ILogger<AudioFileValidator>>();
        _options = new AudioUploadOptions
        {
            RealTimeAllowedExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac" },
            RealTimeMaxFileSizeInBytes = 10 * 1024 * 1024, // 10 MB
            UploadFolderPath = "uploads"
        };

        var mockOptions = new Mock<IOptions<AudioUploadOptions>>();
        mockOptions.Setup(x => x.Value).Returns(_options);

        _validator = new AudioFileValidator(mockOptions.Object, _mockLogger.Object);
    }

    [Fact]
    public void ValidateFile_ShouldThrowException_WhenFileIsNull()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidAudioFileException>(() =>
            _validator.ValidateFile(null!, TranscriptionMode.RealTime));

        exception.Message.Should().Contain("No file uploaded or file is empty");
    }

    [Fact]
    public void ValidateFile_ShouldThrowException_WhenFileIsEmpty()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);
        mockFile.Setup(f => f.FileName).Returns("empty.wav");

        // Act & Assert
        var exception = Assert.Throws<InvalidAudioFileException>(() =>
            _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime));

        exception.Message.Should().Contain("No file uploaded or file is empty");
    }

    [Fact]
    public void ValidateFile_ShouldThrowException_WhenFileSizeExceedsLimit()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        var oversizeLength = _options.RealTimeMaxFileSizeInBytes + 1024;
        mockFile.Setup(f => f.Length).Returns(oversizeLength);
        mockFile.Setup(f => f.FileName).Returns("large.wav");

        // Act & Assert
        var exception = Assert.Throws<InvalidAudioFileException>(() =>
            _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime));

        exception.Message.Should().Contain("File size exceeds");
        exception.Message.Should().Contain("MB");
    }

    [Fact]
    public void ValidateFile_ShouldThrowException_WhenFileExtensionIsNotAllowed()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024 * 1024); // 1 MB
        mockFile.Setup(f => f.FileName).Returns("audio.mp4");

        // Act & Assert
        var exception = Assert.Throws<InvalidAudioFileException>(() =>
            _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime));

        exception.Message.Should().Contain("Invalid file type");
        exception.Message.Should().Contain(".wav");
        exception.Message.Should().Contain(".mp3");
        exception.Message.Should().Contain(".ogg");
        exception.Message.Should().Contain(".flac");
    }

    [Theory]
    [InlineData("audio.WAV")]
    [InlineData("audio.Mp3")]
    [InlineData("audio.OGG")]
    [InlineData("audio.FlAc")]
    public void ValidateFile_ShouldAcceptFile_WhenExtensionIsCaseInsensitive(string fileName)
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024 * 1024); // 1 MB
        mockFile.Setup(f => f.FileName).Returns(fileName);

        // Act & Assert - Should not throw
        _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("File validation passed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("test.wav")]
    [InlineData("test.mp3")]
    [InlineData("test.ogg")]
    [InlineData("test.flac")]
    public void ValidateFile_ShouldAcceptFile_WhenExtensionIsValid(string fileName)
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024 * 1024); // 1 MB
        mockFile.Setup(f => f.FileName).Returns(fileName);

        // Act & Assert - Should not throw
        _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime);
    }

    [Fact]
    public void ValidateFile_ShouldAcceptFile_WhenSizeIsExactlyAtLimit()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(_options.RealTimeMaxFileSizeInBytes);
        mockFile.Setup(f => f.FileName).Returns("maxsize.wav");

        // Act & Assert - Should not throw
        _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime);
    }

    [Fact]
    public void ValidateFile_ShouldAcceptFile_WhenSizeIsOneByteBelowLimit()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(_options.RealTimeMaxFileSizeInBytes - 1);
        mockFile.Setup(f => f.FileName).Returns("justundermax.wav");

        // Act & Assert - Should not throw
        _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime);
    }

    [Fact]
    public void ValidateFile_ShouldLogInformation_WhenFileIsValid()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        var fileSize = 2 * 1024 * 1024; // 2 MB
        mockFile.Setup(f => f.Length).Returns(fileSize);
        mockFile.Setup(f => f.FileName).Returns("valid.wav");

        // Act
        _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime);

        // Assert - Verify logging occurred with file details
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("File validation passed") &&
                    v.ToString()!.Contains("valid.wav") &&
                    v.ToString()!.Contains("KB")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("file.docx")]
    [InlineData("file.exe")]
    [InlineData("file.zip")]
    [InlineData("file")]
    public void ValidateFile_ShouldRejectFile_WhenExtensionIsInvalid(string fileName)
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024 * 1024);
        mockFile.Setup(f => f.FileName).Returns(fileName);

        // Act & Assert
        var exception = Assert.Throws<InvalidAudioFileException>(() =>
            _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime));

        exception.Message.Should().Contain("Invalid file type");
    }

    [Fact]
    public void ValidateFile_ShouldHandleFileNameWithMultipleDots()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024 * 1024);
        mockFile.Setup(f => f.FileName).Returns("my.audio.file.wav");

        // Act & Assert - Should not throw
        _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime);
    }

    [Fact]
    public void ValidateFile_ShouldHandleFileNameWithPath()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024 * 1024);
        mockFile.Setup(f => f.FileName).Returns("C:\\Users\\Test\\audio.wav");

        // Act & Assert - Should not throw (Path.GetExtension handles paths)
        _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime);
    }

    [Theory]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1024 * 1024, "1024.00 KB")]
    [InlineData(5 * 1024 * 1024, "5120.00 KB")]
    public void ValidateFile_ShouldLogCorrectFileSize(long fileSize, string expectedSizeText)
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(fileSize);
        mockFile.Setup(f => f.FileName).Returns("test.wav");

        // Act
        _validator.ValidateFile(mockFile.Object, TranscriptionMode.RealTime);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedSizeText)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

