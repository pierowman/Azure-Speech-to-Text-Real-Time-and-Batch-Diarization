using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using speechtotext.Controllers;
using speechtotext.Exceptions;
using speechtotext.Models;
using speechtotext.Services;
using speechtotext.Validation;
using System.Text.Json;
using Xunit;

namespace speechtotext.Tests.Controllers
{
    public class HomeControllerBatchTests
    {
        private readonly Mock<ILogger<HomeController>> _mockLogger;
        private readonly Mock<ISpeechToTextService> _mockSpeechService;
        private readonly Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<IAudioFileValidator> _mockValidator;
        private readonly Mock<ITranscriptionJobService> _mockJobService;
        private readonly Mock<IBatchTranscriptionService> _mockBatchService;
        private readonly IOptions<AudioUploadOptions> _options;

        public HomeControllerBatchTests()
        {
            _mockLogger = new Mock<ILogger<HomeController>>();
            _mockSpeechService = new Mock<ISpeechToTextService>();
            _mockEnvironment = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            _mockValidator = new Mock<IAudioFileValidator>();
            _mockJobService = new Mock<ITranscriptionJobService>();
            _mockBatchService = new Mock<IBatchTranscriptionService>();
            
            _options = Options.Create(new AudioUploadOptions
            {
                RealTimeAllowedExtensions = new[] { ".wav" },
                RealTimeMaxFileSizeInBytes = 26214400,
                BatchAllowedExtensions = new[] { ".wav", ".mp3", ".flac", ".ogg" },
                BatchMaxFileSizeInBytes = 1073741824,
                BatchMaxFiles = 20,
                UploadFolderPath = "uploads"
            });

            _mockEnvironment.Setup(e => e.WebRootPath).Returns("C:\\TestPath\\wwwroot");
        }

        [Fact]
        public void GetValidationRules_RealTime_ShouldReturnSuccess()
        {
            // Arrange
            var expectedRules = "Real-Time: Single file, WAV, max 25 MB";
            _mockValidator.Setup(v => v.GetValidationRulesSummary(TranscriptionMode.RealTime))
                .Returns(expectedRules);
            
            var controller = CreateController();

            // Act
            var result = controller.GetValidationRules(TranscriptionMode.RealTime);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.True(data.GetProperty("success").GetBoolean());
            Assert.Equal(expectedRules, data.GetProperty("rules").GetString());
        }

        [Fact]
        public void GetValidationRules_Batch_ShouldReturnSuccess()
        {
            // Arrange
            var expectedRules = "Batch: Multiple files, WAV/MP3/FLAC/OGG, max 1024 MB per file";
            _mockValidator.Setup(v => v.GetValidationRulesSummary(TranscriptionMode.Batch))
                .Returns(expectedRules);
            
            var controller = CreateController();

            // Act
            var result = controller.GetValidationRules(TranscriptionMode.Batch);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.True(data.GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldReturnError_WhenNoFilesUploaded()
        {
            // Arrange
            var controller = CreateControllerWithHttpContext(new FormFileCollection());

            // Act - Pass null to simulate no files bound to parameter
            var result = await controller.SubmitBatchTranscription(null!, null, 1, 3, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("No files uploaded", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldReturnError_WhenValidationFails()
        {
            // Arrange
            var files = CreateMockFileCollection(("test.txt", 1024));
            var controller = CreateControllerWithHttpContext(files);
            
            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()))
                .Throws(new InvalidAudioFileException("Invalid file type"));

            // Act
            var result = await controller.SubmitBatchTranscription(null!, null, 1, 3, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("Invalid file type", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldReturnSuccess_WhenJobCreatedSuccessfully()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024 * 1024), ("file2.mp3", 2 * 1024 * 1024));
            var controller = CreateControllerWithHttpContext(files);
            var jobName = "Test Batch Job";
            
            var expectedJob = new TranscriptionJob
            {
                Id = "job-123",
                DisplayName = jobName,
                Status = "NotStarted"
            };

            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedJob);

            // Act
            var result = await controller.SubmitBatchTranscription(null!, jobName, 1, 3, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.True(response.Success);
            Assert.Equal("job-123", response.JobId);
            Assert.Equal(jobName, response.JobName);
            Assert.Equal(2, response.FilesSubmitted);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldUseDefaultJobName_WhenNotProvided()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024 * 1024));
            var controller = CreateControllerWithHttpContext(files);
            
            var capturedJobName = string.Empty;
            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<string>, string, string, bool, int?, int?, CancellationToken>(
                    (paths, name, lang, diar, min, max, ct) => capturedJobName = name)
                .ReturnsAsync(new TranscriptionJob { Id = "job-456", DisplayName = "Default Job" });

            // Act
            await controller.SubmitBatchTranscription(null!, null, 1, 3, null, CancellationToken.None);

            // Assert
            Assert.Contains("Batch Transcription", capturedJobName);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldHandleServiceException()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024 * 1024));
            var controller = CreateControllerWithHttpContext(files);
            
            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Test", 1, 3, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("Service error", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldSaveFiles()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024), ("file2.mp3", 2048));
            var controller = CreateControllerWithHttpContext(files);
            
            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TranscriptionJob { Id = "job-save-test" });

            // Act
            await controller.SubmitBatchTranscription(null!, "Save Test", 1, 3, null, CancellationToken.None);

            // Assert
            _mockBatchService.Verify(s => s.CreateBatchTranscriptionAsync(
                It.Is<IEnumerable<string>>(paths => paths.Count() == 2),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldHandleValidationException()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.mp4", 1024));
            var controller = CreateControllerWithHttpContext(files);
            
            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()))
                .Throws(new InvalidAudioFileException("Invalid file format"));

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Test", 1, 3, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("Invalid file format", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldSupportCancellation()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);
            var cts = new CancellationTokenSource();
            
            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TranscriptionJob { Id = "job-cancel" });

            // Act
            await controller.SubmitBatchTranscription(null!, "Cancel Test", 1, 3, null, cts.Token);

            // Assert
            _mockBatchService.Verify(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                cts.Token),
                Times.Once);
        }

        // NEW TESTS FOR MIN/MAX SPEAKERS

        [Fact]
        public async Task SubmitBatchTranscription_ShouldReturnError_WhenMinSpeakersLessThan1()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Test", 0, 3, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("Minimum speakers must be between 1 and 10", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldReturnError_WhenMinSpeakersGreaterThan10()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Test", 11, 15, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("Minimum speakers must be between 1 and 10", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldReturnError_WhenMaxSpeakersLessThan1()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Test", 1, 0, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("Maximum speakers must be between 1 and 10", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldReturnError_WhenMaxSpeakersGreaterThan10()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Test", 1, 11, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("Maximum speakers must be between 1 and 10", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldReturnError_WhenMinSpeakersGreaterThanMaxSpeakers()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Test", 5, 3, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.False(response.Success);
            Assert.Contains("Minimum speakers cannot be greater than maximum speakers", response.Message);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldPassCorrectSpeakerCounts_ToService()
        {
            // Arrange
            var files = CreateMockFileCollection(("file1.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);
            int? capturedMinSpeakers = null;
            int? capturedMaxSpeakers = null;

            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<string>, string, string, bool, int?, int?, CancellationToken>(
                    (paths, name, lang, diar, min, max, ct) =>
                    {
                        capturedMinSpeakers = min;
                        capturedMaxSpeakers = max;
                    })
                .ReturnsAsync(new TranscriptionJob { Id = "job-test" });

            // Act
            await controller.SubmitBatchTranscription(null!, "Test", 2, 5, null, CancellationToken.None);

            // Assert
            Assert.Equal(2, capturedMinSpeakers);
            Assert.Equal(5, capturedMaxSpeakers);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldAcceptValidSpeakerRange_Interview()
        {
            // Arrange - Interview scenario (2-2 speakers)
            var files = CreateMockFileCollection(("interview.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TranscriptionJob { Id = "job-interview" });

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Interview", 2, 2, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.True(response.Success);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldAcceptValidSpeakerRange_SmallMeeting()
        {
            // Arrange - Small meeting scenario (2-3 speakers)
            var files = CreateMockFileCollection(("meeting.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TranscriptionJob { Id = "job-meeting" });

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Meeting", 2, 3, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.True(response.Success);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldAcceptValidSpeakerRange_LargeMeeting()
        {
            // Arrange - Large meeting scenario (1-10 speakers)
            var files = CreateMockFileCollection(("conference.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TranscriptionJob { Id = "job-conference" });

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Conference", 1, 10, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.True(response.Success);
        }

        [Fact]
        public async Task SubmitBatchTranscription_ShouldAcceptSameSpeakerCountForMinAndMax()
        {
            // Arrange - Exact speaker count known
            var files = CreateMockFileCollection(("known.wav", 1024));
            var controller = CreateControllerWithHttpContext(files);

            _mockValidator.Setup(v => v.ValidateFiles(It.IsAny<IFormFileCollection>()));
            _mockBatchService.Setup(s => s.CreateBatchTranscriptionAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TranscriptionJob { Id = "job-exact" });

            // Act
            var result = await controller.SubmitBatchTranscription(null!, "Exact Count", 4, 4, null, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<BatchTranscriptionResponse>(jsonResult.Value);
            Assert.True(response.Success);
        }

        private HomeController CreateController()
        {
            return new HomeController(
                _mockLogger.Object,
                _mockSpeechService.Object,
                _mockEnvironment.Object,
                _mockValidator.Object,
                _options,
                _mockJobService.Object,
                _mockBatchService.Object);
        }

        private HomeController CreateControllerWithHttpContext(IFormFileCollection files)
        {
            var controller = CreateController();
            
            // Mock HttpContext and Request
            var mockHttpContext = new Mock<HttpContext>();
            var mockRequest = new Mock<HttpRequest>();
            var mockForm = new Mock<IFormCollection>();
            
            // Setup the form to return our file collection
            mockForm.Setup(f => f.Files).Returns(files);
            mockRequest.Setup(r => r.Form).Returns(mockForm.Object);
            mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
            
            // Assign the mocked HttpContext to the controller
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = mockHttpContext.Object
            };
            
            return controller;
        }

        private IFormFileCollection CreateMockFileCollection(params (string fileName, long size)[] fileSpecs)
        {
            var files = new FormFileCollection();
            foreach (var (fileName, size) in fileSpecs)
            {
                var mockFile = new Mock<IFormFile>();
                mockFile.Setup(f => f.FileName).Returns(fileName);
                mockFile.Setup(f => f.Length).Returns(size);
                mockFile.Setup(f => f.Name).Returns("audioFiles");
                mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[size]));
                mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                files.Add(mockFile.Object);
            }
            return files;
        }
    }
}
