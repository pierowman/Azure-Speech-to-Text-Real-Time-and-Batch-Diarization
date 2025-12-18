using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using speechtotext.Controllers;
using speechtotext.Models;
using speechtotext.Services;
using speechtotext.Validation;
using System.Text.Json;
using Xunit;

namespace speechtotext.Tests.Controllers
{
    public class HomeControllerJobsTests
    {
        private readonly Mock<ILogger<HomeController>> _mockLogger;
        private readonly Mock<ISpeechToTextService> _mockSpeechService;
        private readonly Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<IAudioFileValidator> _mockValidator;
        private readonly Mock<ITranscriptionJobService> _mockJobService;
        private readonly Mock<IBatchTranscriptionService> _mockBatchService;
        private readonly IOptions<AudioUploadOptions> _options;

        public HomeControllerJobsTests()
        {
            _mockLogger = new Mock<ILogger<HomeController>>();
            _mockSpeechService = new Mock<ISpeechToTextService>();
            _mockEnvironment = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            _mockValidator = new Mock<IAudioFileValidator>();
            _mockJobService = new Mock<ITranscriptionJobService>();
            _mockBatchService = new Mock<IBatchTranscriptionService>();
            
            _options = Options.Create(new AudioUploadOptions
            {
                RealTimeAllowedExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac" },
                RealTimeMaxFileSizeInBytes = 104857600,
                RealTimeMaxDurationInMinutes = 60,
                UploadFolderPath = "uploads"
            });

            _mockEnvironment.Setup(e => e.WebRootPath).Returns("C:\\TestPath\\wwwroot");
        }

        [Fact]
        public async Task GetTranscriptionJobs_ShouldReturnSuccess_WhenJobsExist()
        {
            // Arrange
            var jobs = new List<TranscriptionJob>
            {
                new TranscriptionJob
                {
                    Id = "job-1",
                    DisplayName = "Test Job 1",
                    Status = "Running",
                    CreatedDateTime = DateTime.UtcNow
                },
                new TranscriptionJob
                {
                    Id = "job-2",
                    DisplayName = "Test Job 2",
                    Status = "Succeeded",
                    CreatedDateTime = DateTime.UtcNow.AddHours(-1)
                }
            };

            _mockJobService.Setup(s => s.GetTranscriptionJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobs);

            var controller = CreateController();

            // Act
            var result = await controller.GetTranscriptionJobs(CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            // Serialize and deserialize to access properties
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.True(data.GetProperty("success").GetBoolean());
            Assert.True(data.TryGetProperty("jobs", out var jobsProperty));
            Assert.Equal(2, jobsProperty.GetArrayLength());
        }

        [Fact]
        public async Task GetTranscriptionJobs_ShouldReturnEmptyList_WhenNoJobsExist()
        {
            // Arrange
            _mockJobService.Setup(s => s.GetTranscriptionJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TranscriptionJob>());

            var controller = CreateController();

            // Act
            var result = await controller.GetTranscriptionJobs(CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.True(data.GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task GetTranscriptionJobs_ShouldReturnError_WhenExceptionOccurs()
        {
            // Arrange
            _mockJobService.Setup(s => s.GetTranscriptionJobsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test exception"));

            var controller = CreateController();

            // Act
            var result = await controller.GetTranscriptionJobs(CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.False(data.GetProperty("success").GetBoolean());
            Assert.Contains("Test exception", data.GetProperty("message").GetString());
        }

        [Fact]
        public async Task CancelTranscriptionJob_ShouldReturnSuccess_WhenCancelSucceeds()
        {
            // Arrange
            var request = new CancelJobRequest { JobId = "job-to-cancel" };
            _mockJobService.Setup(s => s.CancelTranscriptionJobAsync("job-to-cancel", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var controller = CreateController();

            // Act
            var result = await controller.CancelTranscriptionJob(request, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.True(data.GetProperty("success").GetBoolean());
            Assert.Equal("Job canceled successfully", data.GetProperty("message").GetString());
        }

        [Fact]
        public async Task CancelTranscriptionJob_ShouldReturnError_WhenCancelFails()
        {
            // Arrange
            var request = new CancelJobRequest { JobId = "job-to-cancel" };
            _mockJobService.Setup(s => s.CancelTranscriptionJobAsync("job-to-cancel", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var controller = CreateController();

            // Act
            var result = await controller.CancelTranscriptionJob(request, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.False(data.GetProperty("success").GetBoolean());
            Assert.Equal("Failed to cancel job", data.GetProperty("message").GetString());
        }

        [Fact]
        public async Task CancelTranscriptionJob_ShouldReturnBadRequest_WhenJobIdIsEmpty()
        {
            // Arrange
            var request = new CancelJobRequest { JobId = "" };
            var controller = CreateController();

            // Act
            var result = await controller.CancelTranscriptionJob(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
            
            var json = JsonSerializer.Serialize(badRequestResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.False(data.GetProperty("success").GetBoolean());
            Assert.Equal("Job ID is required", data.GetProperty("message").GetString());
        }

        [Fact]
        public async Task CancelTranscriptionJob_ShouldReturnBadRequest_WhenJobIdIsNull()
        {
            // Arrange
            var request = new CancelJobRequest { JobId = null! };
            var controller = CreateController();

            // Act
            var result = await controller.CancelTranscriptionJob(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CancelTranscriptionJob_ShouldReturnError_WhenExceptionOccurs()
        {
            // Arrange
            var request = new CancelJobRequest { JobId = "job-123" };
            _mockJobService.Setup(s => s.CancelTranscriptionJobAsync("job-123", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service unavailable"));

            var controller = CreateController();

            // Act
            var result = await controller.CancelTranscriptionJob(request, CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.False(data.GetProperty("success").GetBoolean());
            Assert.Contains("Service unavailable", data.GetProperty("message").GetString());
        }

        [Fact]
        public async Task GetTranscriptionJobs_ShouldCallServiceWithCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            _mockJobService.Setup(s => s.GetTranscriptionJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TranscriptionJob>());

            var controller = CreateController();

            // Act
            await controller.GetTranscriptionJobs(cts.Token);

            // Assert
            _mockJobService.Verify(
                s => s.GetTranscriptionJobsAsync(It.Is<CancellationToken>(ct => ct == cts.Token)),
                Times.Once);
        }

        [Fact]
        public async Task CancelTranscriptionJob_ShouldCallServiceWithCorrectJobId()
        {
            // Arrange
            var jobId = "specific-job-id-123";
            var request = new CancelJobRequest { JobId = jobId };
            _mockJobService.Setup(s => s.CancelTranscriptionJobAsync(jobId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var controller = CreateController();

            // Act
            await controller.CancelTranscriptionJob(request, CancellationToken.None);

            // Assert
            _mockJobService.Verify(
                s => s.CancelTranscriptionJobAsync(jobId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetTranscriptionJobs_ShouldHandleMultipleJobs()
        {
            // Arrange
            var jobs = new List<TranscriptionJob>();
            for (int i = 0; i < 10; i++)
            {
                jobs.Add(new TranscriptionJob
                {
                    Id = $"job-{i}",
                    DisplayName = $"Job {i}",
                    Status = i % 2 == 0 ? "Succeeded" : "Running",
                    CreatedDateTime = DateTime.UtcNow.AddHours(-i)
                });
            }

            _mockJobService.Setup(s => s.GetTranscriptionJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobs);

            var controller = CreateController();

            // Act
            var result = await controller.GetTranscriptionJobs(CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.True(data.GetProperty("success").GetBoolean());
            Assert.Equal(10, data.GetProperty("jobs").GetArrayLength());
        }

        [Fact]
        public async Task GetTranscriptionJobs_ShouldHandleJobsWithErrors()
        {
            // Arrange
            var jobs = new List<TranscriptionJob>
            {
                new TranscriptionJob
                {
                    Id = "failed-job",
                    DisplayName = "Failed Job",
                    Status = "Failed",
                    Error = "Audio format not supported",
                    CreatedDateTime = DateTime.UtcNow
                }
            };

            _mockJobService.Setup(s => s.GetTranscriptionJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobs);

            var controller = CreateController();

            // Act
            var result = await controller.GetTranscriptionJobs(CancellationToken.None);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
            
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            Assert.True(data.GetProperty("success").GetBoolean());
            
            var jobsArray = data.GetProperty("jobs");
            Assert.Equal(1, jobsArray.GetArrayLength());
            Assert.Equal("Failed", jobsArray[0].GetProperty("Status").GetString());
        }

        [Fact]
        public async Task CancelTranscriptionJob_ShouldLogError_WhenExceptionOccurs()
        {
            // Arrange
            var request = new CancelJobRequest { JobId = "job-123" };
            var exception = new Exception("Service error");
            _mockJobService.Setup(s => s.CancelTranscriptionJobAsync("job-123", It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            var controller = CreateController();

            // Act
            await controller.CancelTranscriptionJob(request, CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error canceling transcription job")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
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
    }
}
