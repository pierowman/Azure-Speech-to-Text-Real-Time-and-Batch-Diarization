using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using speechtotext.Exceptions;
using speechtotext.Models;
using speechtotext.Validation;
using Xunit;

namespace speechtotext.Tests.Validation
{
    public class AudioFileValidatorModeTests
    {
        private readonly Mock<ILogger<AudioFileValidator>> _mockLogger;
        private readonly AudioUploadOptions _options;

        public AudioFileValidatorModeTests()
        {
            _mockLogger = new Mock<ILogger<AudioFileValidator>>();
            _options = new AudioUploadOptions
            {
                RealTimeAllowedExtensions = new[] { ".wav" },
                RealTimeMaxFileSizeInBytes = 25 * 1024 * 1024,
                RealTimeMaxDurationInMinutes = 60,
                BatchAllowedExtensions = new[] { ".wav", ".mp3", ".flac", ".ogg" },
                BatchMaxFileSizeInBytes = 1024 * 1024 * 1024,
                BatchMaxDurationInMinutes = 480,
                BatchMaxFiles = 20
            };
        }

        [Fact]
        public void ValidateFile_RealTimeMode_ShouldAcceptWavFile()
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.wav", 10 * 1024 * 1024); // 10 MB

            // Act & Assert (no exception)
            validator.ValidateFile(file, TranscriptionMode.RealTime);
        }

        [Fact]
        public void ValidateFile_RealTimeMode_ShouldRejectMp3File()
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.mp3", 10 * 1024 * 1024);

            // Act & Assert
            var exception = Assert.Throws<InvalidAudioFileException>(() =>
                validator.ValidateFile(file, TranscriptionMode.RealTime));
            
            Assert.Contains("Real-Time", exception.Message);
            Assert.Contains("Batch mode supports more formats", exception.Message);
        }

        [Fact]
        public void ValidateFile_RealTimeMode_ShouldRejectLargeFile()
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.wav", 30 * 1024 * 1024); // 30 MB

            // Act & Assert
            var exception = Assert.Throws<InvalidAudioFileException>(() =>
                validator.ValidateFile(file, TranscriptionMode.RealTime));
            
            Assert.Contains("Real-Time mode maximum", exception.Message);
            Assert.Contains("Consider using Batch mode", exception.Message);
        }

        [Fact]
        public void ValidateFile_BatchMode_ShouldAcceptWavFile()
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.wav", 100 * 1024 * 1024); // 100 MB

            // Act & Assert (no exception)
            validator.ValidateFile(file, TranscriptionMode.Batch);
        }

        [Fact]
        public void ValidateFile_BatchMode_ShouldAcceptMp3File()
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.mp3", 100 * 1024 * 1024);

            // Act & Assert (no exception)
            validator.ValidateFile(file, TranscriptionMode.Batch);
        }

        [Fact]
        public void ValidateFile_BatchMode_ShouldAcceptFlacFile()
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.flac", 100 * 1024 * 1024);

            // Act & Assert (no exception)
            validator.ValidateFile(file, TranscriptionMode.Batch);
        }

        [Fact]
        public void ValidateFile_BatchMode_ShouldAcceptOggFile()
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.ogg", 100 * 1024 * 1024);

            // Act & Assert (no exception)
            validator.ValidateFile(file, TranscriptionMode.Batch);
        }

        [Fact]
        public void ValidateFile_BatchMode_ShouldRejectExcessivelyLargeFile()
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.wav", 2L * 1024 * 1024 * 1024); // 2 GB

            // Act & Assert
            var exception = Assert.Throws<InvalidAudioFileException>(() =>
                validator.ValidateFile(file, TranscriptionMode.Batch));
            
            Assert.Contains("Batch mode maximum", exception.Message);
        }

        [Fact]
        public void ValidateFiles_ShouldAcceptMultipleFiles()
        {
            // Arrange
            var validator = CreateValidator();
            var files = CreateMockFileCollection(
                ("file1.wav", 10 * 1024 * 1024),
                ("file2.mp3", 20 * 1024 * 1024),
                ("file3.flac", 30 * 1024 * 1024)
            );

            // Act & Assert (no exception)
            validator.ValidateFiles(files);
        }

        [Fact]
        public void ValidateFiles_ShouldRejectTooManyFiles()
        {
            // Arrange
            var validator = CreateValidator();
            var fileSpecs = Enumerable.Range(1, 25)
                .Select(i => (fileName: $"file{i}.wav", size: (long)(10 * 1024 * 1024)))
                .ToArray();
            var files = CreateMockFileCollection(fileSpecs);

            // Act & Assert
            var exception = Assert.Throws<InvalidAudioFileException>(() =>
                validator.ValidateFiles(files));
            
            Assert.Contains("Too many files", exception.Message);
            Assert.Contains("20 files per job", exception.Message);
        }

        [Fact]
        public void ValidateFiles_ShouldRejectEmptyCollection()
        {
            // Arrange
            var validator = CreateValidator();
            var files = new FormFileCollection();

            // Act & Assert
            var exception = Assert.Throws<InvalidAudioFileException>(() =>
                validator.ValidateFiles(files));
            
            Assert.Contains("No files uploaded", exception.Message);
        }

        [Fact]
        public void ValidateFiles_ShouldRejectIfAnyFileIsInvalid()
        {
            // Arrange
            var validator = CreateValidator();
            var files = CreateMockFileCollection(
                ("file1.wav", 10 * 1024 * 1024),
                ("file2.txt", 5 * 1024 * 1024), // Invalid format
                ("file3.mp3", 20 * 1024 * 1024)
            );

            // Act & Assert
            var exception = Assert.Throws<InvalidAudioFileException>(() =>
                validator.ValidateFiles(files));
            
            Assert.Contains("Validation failed", exception.Message);
            Assert.Contains("file2.txt", exception.Message);
        }

        [Fact]
        public void GetValidationRulesSummary_RealTimeMode_ShouldReturnCorrectSummary()
        {
            // Arrange
            var validator = CreateValidator();

            // Act
            var summary = validator.GetValidationRulesSummary(TranscriptionMode.RealTime);

            // Assert
            Assert.Contains("Real-Time", summary);
            Assert.Contains("Single file", summary);
            Assert.Contains("WAV", summary);
            Assert.Contains("25 MB", summary);
            Assert.Contains("immediately", summary);
        }

        [Fact]
        public void GetValidationRulesSummary_BatchMode_ShouldReturnCorrectSummary()
        {
            // Arrange
            var validator = CreateValidator();

            // Act
            var summary = validator.GetValidationRulesSummary(TranscriptionMode.Batch);

            // Assert
            Assert.Contains("Batch", summary);
            Assert.Contains("Multiple files", summary);
            Assert.Contains("20", summary);
            Assert.Contains("WAV", summary);
            Assert.Contains("MP3", summary);
            Assert.Contains("1024 MB", summary);
            Assert.Contains("Transcription Jobs", summary);
        }

        [Theory]
        [InlineData(TranscriptionMode.RealTime)]
        [InlineData(TranscriptionMode.Batch)]
        public void ValidateFile_ShouldRejectNullFile(TranscriptionMode mode)
        {
            // Arrange
            var validator = CreateValidator();

            // Act & Assert
            var exception = Assert.Throws<InvalidAudioFileException>(() =>
                validator.ValidateFile(null!, mode));
            
            Assert.Contains("No file uploaded", exception.Message);
        }

        [Theory]
        [InlineData(TranscriptionMode.RealTime)]
        [InlineData(TranscriptionMode.Batch)]
        public void ValidateFile_ShouldRejectEmptyFile(TranscriptionMode mode)
        {
            // Arrange
            var validator = CreateValidator();
            var file = CreateMockFile("test.wav", 0);

            // Act & Assert
            var exception = Assert.Throws<InvalidAudioFileException>(() =>
                validator.ValidateFile(file, mode));
            
            Assert.Contains("empty", exception.Message);
        }

        private AudioFileValidator CreateValidator()
        {
            var options = Options.Create(_options);
            return new AudioFileValidator(options, _mockLogger.Object);
        }

        private IFormFile CreateMockFile(string fileName, long size)
        {
            var mock = new Mock<IFormFile>();
            mock.Setup(f => f.FileName).Returns(fileName);
            mock.Setup(f => f.Length).Returns(size);
            return mock.Object;
        }

        private IFormFileCollection CreateMockFileCollection(params (string fileName, long size)[] fileSpecs)
        {
            var files = new FormFileCollection();
            foreach (var (fileName, size) in fileSpecs)
            {
                files.Add(CreateMockFile(fileName, size));
            }
            return files;
        }
    }
}
