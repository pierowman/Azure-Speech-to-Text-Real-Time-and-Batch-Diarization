using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using speechtotext.Controllers;
using speechtotext.Models;
using speechtotext.Services;
using speechtotext.Validation;
using Xunit;
using FluentAssertions;

namespace speechtotext.Tests.Controllers
{
    public class HomeControllerPerFileResultsTests
    {
        private readonly Mock<ILogger<HomeController>> _mockLogger;
        private readonly Mock<ISpeechToTextService> _mockSpeechService;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<IAudioFileValidator> _mockValidator;
        private readonly Mock<ITranscriptionJobService> _mockJobService;
        private readonly Mock<IBatchTranscriptionService> _mockBatchService;
        private readonly IOptions<AudioUploadOptions> _options;

        public HomeControllerPerFileResultsTests()
        {
            _mockLogger = new Mock<ILogger<HomeController>>();
            _mockSpeechService = new Mock<ISpeechToTextService>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockValidator = new Mock<IAudioFileValidator>();
            _mockJobService = new Mock<ITranscriptionJobService>();
            _mockBatchService = new Mock<IBatchTranscriptionService>();
            
            _options = Options.Create(new AudioUploadOptions
            {
                ShowTranscriptionJobsTab = true,
                EnableBatchTranscription = true
            });
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldReturnCombinedResults_WhenFileIndexNotProvided()
        {
            // Arrange
            var controller = CreateController();
            var expectedResult = CreateSampleBatchResult(totalFiles: 2);
            
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsAsync("job-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await controller.GetBatchTranscriptionResults("job-123", null, CancellationToken.None);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            var data = jsonResult.Value.Should().BeAssignableTo<BatchTranscriptionResult>().Subject;
            data.Success.Should().BeTrue();
            data.TotalFiles.Should().Be(2);
            
            _mockJobService.Verify(s => s.GetTranscriptionResultsAsync("job-123", It.IsAny<CancellationToken>()), Times.Once);
            _mockJobService.Verify(s => s.GetTranscriptionResultsByFileAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldReturnFileSpecificResults_WhenFileIndexProvided()
        {
            // Arrange
            var controller = CreateController();
            var expectedResult = CreateSampleBatchResult(totalFiles: 1);
            expectedResult.DisplayName = "Test Job - File 1";
            
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsByFileAsync("job-123", 0, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await controller.GetBatchTranscriptionResults("job-123", 0, CancellationToken.None);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            var data = jsonResult.Value.Should().BeAssignableTo<BatchTranscriptionResult>().Subject;
            data.Success.Should().BeTrue();
            data.DisplayName.Should().Contain("File 1");
            
            _mockJobService.Verify(s => s.GetTranscriptionResultsByFileAsync("job-123", 0, It.IsAny<CancellationToken>()), Times.Once);
            _mockJobService.Verify(s => s.GetTranscriptionResultsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldReturnBadRequest_WhenJobIdEmpty()
        {
            // Arrange
            var controller = CreateController();

            // Act
            var result = await controller.GetBatchTranscriptionResults("", null, CancellationToken.None);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var value = badRequestResult.Value;
            value.Should().NotBeNull();
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldReturnBadRequest_WhenJobIdNull()
        {
            // Arrange
            var controller = CreateController();

            // Act
            var result = await controller.GetBatchTranscriptionResults(null!, null, CancellationToken.None);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldReturnError_WhenServiceReturnsFailure()
        {
            // Arrange
            var controller = CreateController();
            var failedResult = new BatchTranscriptionResult
            {
                Success = false,
                Message = "Job not found"
            };
            
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsAsync("job-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedResult);

            // Act
            var result = await controller.GetBatchTranscriptionResults("job-123", null, CancellationToken.None);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            var data = jsonResult.Value;
            
            // Using reflection to check dynamic object properties
            var successProp = data!.GetType().GetProperty("success");
            var messageProp = data!.GetType().GetProperty("message");
            
            successProp!.GetValue(data).Should().Be(false);
            messageProp!.GetValue(data).Should().Be("Job not found");
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldReturnError_WhenServiceReturnsNull()
        {
            // Arrange
            var controller = CreateController();
            
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsAsync("job-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync((BatchTranscriptionResult?)null);

            // Act
            var result = await controller.GetBatchTranscriptionResults("job-123", null, CancellationToken.None);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            var data = jsonResult.Value;
            
            var successProp = data!.GetType().GetProperty("success");
            successProp!.GetValue(data).Should().Be(false);
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldHandleMultipleFileIndices()
        {
            // Arrange
            var controller = CreateController();
            var file0Result = CreateSampleBatchResult(totalFiles: 1);
            file0Result.DisplayName = "Job - File 1";
            var file1Result = CreateSampleBatchResult(totalFiles: 1);
            file1Result.DisplayName = "Job - File 2";
            
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsByFileAsync("job-123", 0, It.IsAny<CancellationToken>()))
                .ReturnsAsync(file0Result);
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsByFileAsync("job-123", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(file1Result);

            // Act
            var result0 = await controller.GetBatchTranscriptionResults("job-123", 0, CancellationToken.None);
            var result1 = await controller.GetBatchTranscriptionResults("job-123", 1, CancellationToken.None);

            // Assert
            var jsonResult0 = result0.Should().BeOfType<JsonResult>().Subject;
            var data0 = jsonResult0.Value.Should().BeAssignableTo<BatchTranscriptionResult>().Subject;
            data0.DisplayName.Should().Contain("File 1");

            var jsonResult1 = result1.Should().BeOfType<JsonResult>().Subject;
            var data1 = jsonResult1.Value.Should().BeAssignableTo<BatchTranscriptionResult>().Subject;
            data1.DisplayName.Should().Contain("File 2");
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldReturnError_ForInvalidFileIndex()
        {
            // Arrange
            var controller = CreateController();
            var errorResult = new BatchTranscriptionResult
            {
                Success = false,
                Message = "Invalid file index"
            };
            
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsByFileAsync("job-123", 99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorResult);

            // Act
            var result = await controller.GetBatchTranscriptionResults("job-123", 99, CancellationToken.None);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            var data = jsonResult.Value;
            
            var successProp = data!.GetType().GetProperty("success");
            var messageProp = data!.GetType().GetProperty("message");
            
            successProp!.GetValue(data).Should().Be(false);
            messageProp!.GetValue(data).Should().Be("Invalid file index");
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldLogInformation_WhenCalled()
        {
            // Arrange
            var controller = CreateController();
            var expectedResult = CreateSampleBatchResult(totalFiles: 2);
            
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsAsync("job-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            await controller.GetBatchTranscriptionResults("job-123", null, CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fetching batch transcription results")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetBatchTranscriptionResults_ShouldHandleExceptions()
        {
            // Arrange
            var controller = CreateController();
            
            _mockJobService
                .Setup(s => s.GetTranscriptionResultsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network error"));

            // Act
            var result = await controller.GetBatchTranscriptionResults("job-123", null, CancellationToken.None);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            var data = jsonResult.Value;
            
            var successProp = data!.GetType().GetProperty("success");
            var messageProp = data!.GetType().GetProperty("message");
            
            successProp!.GetValue(data).Should().Be(false);
            messageProp!.GetValue(data).Should().NotBeNull();
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

        private BatchTranscriptionResult CreateSampleBatchResult(int totalFiles)
        {
            var result = new BatchTranscriptionResult
            {
                Success = true,
                Message = "Success",
                JobId = "job-123",
                DisplayName = "Test Job",
                TotalFiles = totalFiles,
                Segments = new List<SpeakerSegment>
                {
                    new() { Speaker = "Speaker 1", Text = "Test" }
                }
            };

            for (int i = 0; i < totalFiles; i++)
            {
                result.FileResults.Add(new FileTranscriptionInfo
                {
                    FileName = $"File {i + 1}",
                    Channel = i,
                    Segments = new List<SpeakerSegment>
                    {
                        new() { Speaker = $"Speaker {i + 1}", Text = "Test" }
                    }
                });
            }

            return result;
        }
    }
}
