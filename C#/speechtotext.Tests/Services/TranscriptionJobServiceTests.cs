using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using speechtotext.Services;
using System.Net;
using System.Text;
using Xunit;

namespace speechtotext.Tests.Services
{
    public class TranscriptionJobServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<TranscriptionJobService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

        public TranscriptionJobServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<TranscriptionJobService>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            // Setup configuration
            _mockConfiguration.Setup(c => c["AzureSpeech:SubscriptionKey"]).Returns("test-key");
            _mockConfiguration.Setup(c => c["AzureSpeech:Region"]).Returns("eastus");
        }

        [Fact]
        public void TranscriptionJobService_Constructor_ShouldThrowException_WhenSubscriptionKeyMissing()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["AzureSpeech:SubscriptionKey"]).Returns((string?)null);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new TranscriptionJobService(_mockConfiguration.Object, _mockLogger.Object, _mockHttpClientFactory.Object));

            Assert.Contains("subscription key not found", exception.Message);
        }

        [Fact]
        public void TranscriptionJobService_Constructor_ShouldThrowException_WhenRegionMissing()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["AzureSpeech:Region"]).Returns((string?)null);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new TranscriptionJobService(_mockConfiguration.Object, _mockLogger.Object, _mockHttpClientFactory.Object));

            Assert.Contains("region not found", exception.Message);
        }

        [Fact]
        public async Task GetTranscriptionJobsAsync_ShouldReturnEmptyList_WhenApiReturnsError()
        {
            // Arrange
            SetupHttpClient(HttpStatusCode.InternalServerError, "{}");
            var service = CreateService();

            // Act
            var result = await service.GetTranscriptionJobsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTranscriptionJobsAsync_ShouldReturnJobs_WhenApiReturnsValidData()
        {
            // Arrange
            var jsonResponse = @"{
                ""values"": [
                    {
                        ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-123"",
                        ""displayName"": ""Test Job"",
                        ""status"": ""Running"",
                        ""createdDateTime"": ""2024-01-01T10:00:00Z"",
                        ""lastActionDateTime"": ""2024-01-01T10:05:00Z"",
                        ""contentUrls"": [""https://example.com/file1.wav""]
                    }
                ]
            }";

            SetupHttpClient(HttpStatusCode.OK, jsonResponse);
            var service = CreateService();

            // Act
            var result = await service.GetTranscriptionJobsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("job-123", result[0].Id);
            Assert.Equal("Test Job", result[0].DisplayName);
            Assert.Equal("Running", result[0].Status);
        }

        [Fact]
        public async Task GetTranscriptionJobAsync_ShouldReturnNull_WhenJobNotFound()
        {
            // Arrange
            SetupHttpClient(HttpStatusCode.NotFound, "{}");
            var service = CreateService();

            // Act
            var result = await service.GetTranscriptionJobAsync("non-existent-job");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTranscriptionJobAsync_ShouldReturnJob_WhenJobExists()
        {
            // Arrange
            var jsonResponse = @"{
                ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-456"",
                ""displayName"": ""Specific Job"",
                ""status"": ""Succeeded"",
                ""createdDateTime"": ""2024-01-01T10:00:00Z""
            }";

            SetupHttpClient(HttpStatusCode.OK, jsonResponse);
            var service = CreateService();

            // Act
            var result = await service.GetTranscriptionJobAsync("job-456");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("job-456", result.Id);
            Assert.Equal("Specific Job", result.DisplayName);
            Assert.Equal("Succeeded", result.Status);
        }

        [Fact]
        public async Task CancelTranscriptionJobAsync_ShouldReturnTrue_WhenCancelSucceeds()
        {
            // Arrange
            SetupHttpClientForDelete(HttpStatusCode.NoContent);
            var service = CreateService();

            // Act
            var result = await service.CancelTranscriptionJobAsync("job-to-cancel");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CancelTranscriptionJobAsync_ShouldReturnFalse_WhenCancelFails()
        {
            // Arrange
            SetupHttpClientForDelete(HttpStatusCode.InternalServerError);
            var service = CreateService();

            // Act
            var result = await service.CancelTranscriptionJobAsync("job-to-cancel");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteTranscriptionJobAsync_ShouldReturnTrue_WhenDeleteSucceeds()
        {
            // Arrange
            SetupHttpClientForDelete(HttpStatusCode.NoContent);
            var service = CreateService();

            // Act
            var result = await service.DeleteTranscriptionJobAsync("job-to-delete");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteTranscriptionJobAsync_ShouldReturnFalse_WhenDeleteFails()
        {
            // Arrange
            SetupHttpClientForDelete(HttpStatusCode.BadRequest);
            var service = CreateService();

            // Act
            var result = await service.DeleteTranscriptionJobAsync("job-to-delete");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetTranscriptionJobsAsync_ShouldHandleEmptyValuesArray()
        {
            // Arrange
            var jsonResponse = @"{""values"": []}";
            SetupHttpClient(HttpStatusCode.OK, jsonResponse);
            var service = CreateService();

            // Act
            var result = await service.GetTranscriptionJobsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTranscriptionJobsAsync_ShouldHandleMultipleJobs()
        {
            // Arrange
            var jsonResponse = @"{
                ""values"": [
                    {
                        ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-1"",
                        ""displayName"": ""Job 1"",
                        ""status"": ""Succeeded"",
                        ""createdDateTime"": ""2024-01-01T10:00:00Z""
                    },
                    {
                        ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-2"",
                        ""displayName"": ""Job 2"",
                        ""status"": ""Running"",
                        ""createdDateTime"": ""2024-01-01T11:00:00Z""
                    },
                    {
                        ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-3"",
                        ""displayName"": ""Job 3"",
                        ""status"": ""Failed"",
                        ""createdDateTime"": ""2024-01-01T09:00:00Z""
                    }
                ]
            }";

            SetupHttpClient(HttpStatusCode.OK, jsonResponse);
            var service = CreateService();

            // Act
            var result = await service.GetTranscriptionJobsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            // Should be ordered by created date descending
            Assert.Equal("job-2", result[0].Id); // Most recent
            Assert.Equal("job-1", result[1].Id);
            Assert.Equal("job-3", result[2].Id); // Oldest
        }

        [Fact]
        public async Task GetTranscriptionJobsAsync_ShouldHandleJobsWithErrors()
        {
            // Arrange
            var jsonResponse = @"{
                ""values"": [
                    {
                        ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-error"",
                        ""displayName"": ""Failed Job"",
                        ""status"": ""Failed"",
                        ""createdDateTime"": ""2024-01-01T10:00:00Z"",
                        ""properties"": {
                            ""error"": {
                                ""message"": ""Audio file format not supported""
                            }
                        }
                    }
                ]
            }";

            SetupHttpClient(HttpStatusCode.OK, jsonResponse);
            var service = CreateService();

            // Act
            var result = await service.GetTranscriptionJobsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Failed", result[0].Status);
            Assert.Equal("Audio file format not supported", result[0].Error);
        }

        [Fact]
        public async Task GetTranscriptionJobsAsync_ShouldHandleJobsWithFiles()
        {
            // Arrange
            var jsonResponse = @"{
                ""values"": [
                    {
                        ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-files"",
                        ""displayName"": ""Multi-file Job"",
                        ""status"": ""Running"",
                        ""createdDateTime"": ""2024-01-01T10:00:00Z"",
                        ""contentUrls"": [
                            ""https://example.com/audio1.wav"",
                            ""https://example.com/audio2.mp3""
                        ]
                    }
                ]
            }";

            SetupHttpClient(HttpStatusCode.OK, jsonResponse);
            var service = CreateService();

            // Act
            var result = await service.GetTranscriptionJobsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(2, result[0].Files.Count);
            Assert.Contains("audio1.wav", result[0].Files);
            Assert.Contains("audio2.mp3", result[0].Files);
        }

        private void SetupHttpClient(HttpStatusCode statusCode, string responseContent)
        {
            var response = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        }

        private void SetupHttpClientForDelete(HttpStatusCode statusCode)
        {
            var response = new HttpResponseMessage { StatusCode = statusCode };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        }

        private TranscriptionJobService CreateService()
        {
            return new TranscriptionJobService(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _mockHttpClientFactory.Object);
        }
    }
}
