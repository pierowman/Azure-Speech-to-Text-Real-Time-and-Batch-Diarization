using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using speechtotext.Controllers;
using speechtotext.Models;
using speechtotext.Services;
using speechtotext.Validation;
using Xunit;

namespace speechtotext.Tests.Controllers
{
    /// <summary>
    /// Tests for the ShowTranscriptionJobsTab configuration feature
    /// </summary>
    public class HomeControllerJobsConfigurationTests
    {
        private readonly Mock<ILogger<HomeController>> _mockLogger;
        private readonly Mock<ISpeechToTextService> _mockSpeechService;
        private readonly Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<IAudioFileValidator> _mockValidator;
        private readonly Mock<ITranscriptionJobService> _mockJobService;
        private readonly Mock<IBatchTranscriptionService> _mockBatchService;

        public HomeControllerJobsConfigurationTests()
        {
            _mockLogger = new Mock<ILogger<HomeController>>();
            _mockSpeechService = new Mock<ISpeechToTextService>();
            _mockEnvironment = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            _mockValidator = new Mock<IAudioFileValidator>();
            _mockJobService = new Mock<ITranscriptionJobService>();
            _mockBatchService = new Mock<IBatchTranscriptionService>();
            _mockEnvironment.Setup(e => e.WebRootPath).Returns("C:\\TestPath\\wwwroot");
        }

        [Fact]
        public void Index_ShouldSetShowTranscriptionJobsTab_ToTrue_WhenConfiguredTrue()
        {
            // Arrange
            var options = Options.Create(new AudioUploadOptions
            {
                RealTimeAllowedExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac" },
                RealTimeMaxFileSizeInBytes = 104857600,
                RealTimeMaxDurationInMinutes = 60,
                UploadFolderPath = "uploads",
                ShowTranscriptionJobsTab = true
            });

            var controller = new HomeController(
                _mockLogger.Object,
                _mockSpeechService.Object,
                _mockEnvironment.Object,
                _mockValidator.Object,
                options,
                _mockJobService.Object,
                _mockBatchService.Object);

            // Act
            var result = controller.Index() as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData.Should().ContainKey("ShowTranscriptionJobsTab");
            result.ViewData["ShowTranscriptionJobsTab"].Should().Be(true);
        }

        [Fact]
        public void Index_ShouldSetShowTranscriptionJobsTab_ToFalse_WhenConfiguredFalse()
        {
            // Arrange
            var options = Options.Create(new AudioUploadOptions
            {
                RealTimeAllowedExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac" },
                RealTimeMaxFileSizeInBytes = 104857600,
                RealTimeMaxDurationInMinutes = 60,
                UploadFolderPath = "uploads",
                ShowTranscriptionJobsTab = false
            });

            var controller = new HomeController(
                _mockLogger.Object,
                _mockSpeechService.Object,
                _mockEnvironment.Object,
                _mockValidator.Object,
                options,
                _mockJobService.Object,
                _mockBatchService.Object);

            // Act
            var result = controller.Index() as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData.Should().ContainKey("ShowTranscriptionJobsTab");
            result.ViewData["ShowTranscriptionJobsTab"].Should().Be(false);
        }

        [Fact]
        public void Index_ShouldDefaultToTrue_WhenNotConfigured()
        {
            // Arrange - Default value should be true
            var options = Options.Create(new AudioUploadOptions
            {
                RealTimeAllowedExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac" },
                RealTimeMaxFileSizeInBytes = 104857600,
                RealTimeMaxDurationInMinutes = 60,
                UploadFolderPath = "uploads"
                // ShowTranscriptionJobsTab not explicitly set, should default to true
            });

            var controller = new HomeController(
                _mockLogger.Object,
                _mockSpeechService.Object,
                _mockEnvironment.Object,
                _mockValidator.Object,
                options,
                _mockJobService.Object,
                _mockBatchService.Object);

            // Act
            var result = controller.Index() as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData.Should().ContainKey("ShowTranscriptionJobsTab");
            result.ViewData["ShowTranscriptionJobsTab"].Should().Be(true);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Index_ShouldRespectConfigurationValue(bool showJobsTab)
        {
            // Arrange
            var options = Options.Create(new AudioUploadOptions
            {
                RealTimeAllowedExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac" },
                RealTimeMaxFileSizeInBytes = 104857600,
                RealTimeMaxDurationInMinutes = 60,
                UploadFolderPath = "uploads",
                ShowTranscriptionJobsTab = showJobsTab
            });

            var controller = new HomeController(
                _mockLogger.Object,
                _mockSpeechService.Object,
                _mockEnvironment.Object,
                _mockValidator.Object,
                options,
                _mockJobService.Object,
                _mockBatchService.Object);

            // Act
            var result = controller.Index() as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewData["ShowTranscriptionJobsTab"].Should().Be(showJobsTab);
        }
    }
}
