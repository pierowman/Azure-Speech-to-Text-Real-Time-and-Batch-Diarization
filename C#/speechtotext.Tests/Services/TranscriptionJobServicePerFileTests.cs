using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using speechtotext.Models;
using speechtotext.Services;
using System.Net;
using System.Text;
using Xunit;
using FluentAssertions;

namespace speechtotext.Tests.Services
{
    public class TranscriptionJobServicePerFileTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<TranscriptionJobService>> _mockLogger;

        public TranscriptionJobServicePerFileTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<TranscriptionJobService>>();

            // Setup configuration
            _mockConfiguration.Setup(c => c["AzureSpeech:SubscriptionKey"]).Returns("test-key");
            _mockConfiguration.Setup(c => c["AzureSpeech:Region"]).Returns("eastus");
        }

        [Fact]
        public async Task GetTranscriptionResultsAsync_ShouldReturnCombinedResults_ForMultipleFiles()
        {
            // Arrange
            var jobJson = CreateSucceededJobJson();
            var filesJson = CreateFilesListJson();
            var transcriptionJson = CreateMultiFileTranscriptionJson();

            var mockFactory = CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsAsync("job-123");

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.TotalFiles.Should().Be(2);
            result.FileResults.Should().HaveCount(2);
            result.Segments.Should().NotBeEmpty();
            result.FileResults[0].FileName.Should().Be("File 1");
            result.FileResults[1].FileName.Should().Be("File 2");
        }

        [Fact]
        public async Task GetTranscriptionResultsAsync_ShouldReturnSingleFileResult_ForSingleFile()
        {
            // Arrange
            var jobJson = CreateSucceededJobJson();
            var filesJson = CreateFilesListJson();
            var transcriptionJson = CreateSingleFileTranscriptionJson();

            var mockFactory = CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsAsync("job-123");

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.TotalFiles.Should().Be(1);
            result.FileResults.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetTranscriptionResultsByFileAsync_ShouldReturnSpecificFileResults()
        {
            // Arrange
            var jobJson = CreateSucceededJobJson();
            var filesJson = CreateFilesListJson();
            var transcriptionJson = CreateMultiFileTranscriptionJson();

            var mockFactory = CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsByFileAsync("job-123", 0);

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Segments.Should().NotBeEmpty();
            result.FileResults.Should().HaveCount(1);
            result.FileResults[0].FileName.Should().Be("File 1");
            result.FileResults[0].Channel.Should().Be(0);
        }

        [Fact]
        public async Task GetTranscriptionResultsByFileAsync_ShouldReturnSecondFileResults()
        {
            // Arrange
            var jobJson = CreateSucceededJobJson();
            var filesJson = CreateFilesListJson();
            var transcriptionJson = CreateMultiFileTranscriptionJson();

            var mockFactory = CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsByFileAsync("job-123", 1);

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Segments.Should().NotBeEmpty();
            result.FileResults.Should().HaveCount(1);
            result.FileResults[0].FileName.Should().Be("File 2");
            result.FileResults[0].Channel.Should().Be(1);
        }

        [Fact]
        public async Task GetTranscriptionResultsByFileAsync_ShouldReturnError_ForInvalidFileIndex()
        {
            // Arrange
            var jobJson = CreateSucceededJobJson();
            var filesJson = CreateFilesListJson();
            var transcriptionJson = CreateMultiFileTranscriptionJson();

            var mockFactory = CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsByFileAsync("job-123", 5); // Invalid index

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Invalid file index");
        }

        [Fact]
        public async Task GetTranscriptionResultsByFileAsync_ShouldReturnError_ForNegativeFileIndex()
        {
            // Arrange
            var jobJson = CreateSucceededJobJson();
            var filesJson = CreateFilesListJson();
            var transcriptionJson = CreateMultiFileTranscriptionJson();

            var mockFactory = CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsByFileAsync("job-123", -1); // Negative index

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("Invalid file index");
        }

        [Fact]
        public async Task GetTranscriptionResultsAsync_ShouldCalculateFileSpecificStatistics()
        {
            // Arrange
            var jobJson = CreateSucceededJobJson();
            var filesJson = CreateFilesListJson();
            var transcriptionJson = CreateMultiFileTranscriptionJson();

            var mockFactory = CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsAsync("job-123");

            // Assert
            result.Should().NotBeNull();
            result!.FileResults.Should().HaveCount(2);
            
            // Each file should have its own speaker statistics
            result.FileResults[0].SpeakerStatistics.Should().NotBeEmpty();
            result.FileResults[1].SpeakerStatistics.Should().NotBeEmpty();
            
            // Each file should have its own available speakers
            result.FileResults[0].AvailableSpeakers.Should().NotBeEmpty();
            result.FileResults[1].AvailableSpeakers.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetTranscriptionResultsByFileAsync_ShouldHandleSingleFileJob()
        {
            // Arrange
            var jobJson = CreateSucceededJobJson();
            var filesJson = CreateFilesListJson();
            var transcriptionJson = CreateSingleFileTranscriptionJson();

            var mockFactory = CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsByFileAsync("job-123", 0);

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.TotalFiles.Should().Be(1);
            result.FileResults.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetTranscriptionResultsAsync_ShouldReturnError_WhenJobNotSucceeded()
        {
            // Arrange
            var jobJson = CreateRunningJobJson();
            var mockFactory = SetupHttpClientFactoryForSingleRequest(jobJson);
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsAsync("job-123");

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("not completed");
        }

        [Fact]
        public async Task GetTranscriptionResultsAsync_ShouldReturnError_WhenJobNotFound()
        {
            // Arrange
            var mockFactory = SetupHttpClientFactoryForNotFound();
            var service = CreateService(mockFactory);

            // Act
            var result = await service.GetTranscriptionResultsAsync("non-existent-job");

            // Assert
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }

        private string CreateSucceededJobJson()
        {
            return @"{
                ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-123"",
                ""displayName"": ""Test Job"",
                ""status"": ""Succeeded"",
                ""createdDateTime"": ""2024-01-01T10:00:00Z""
            }";
        }

        private string CreateRunningJobJson()
        {
            return @"{
                ""self"": ""https://eastus.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions/job-123"",
                ""displayName"": ""Test Job"",
                ""status"": ""Running"",
                ""createdDateTime"": ""2024-01-01T10:00:00Z""
            }";
        }

        private string CreateFilesListJson()
        {
            return @"{
                ""values"": [
                    {
                        ""kind"": ""Transcription"",
                        ""links"": {
                            ""contentUrl"": ""https://example.com/transcription.json""
                        }
                    }
                ]
            }";
        }

        private string CreateMultiFileTranscriptionJson()
        {
            return @"{
                ""recognizedPhrases"": [
                    {
                        ""channel"": 0,
                        ""speaker"": 1,
                        ""offsetInTicks"": 0,
                        ""durationInTicks"": 10000000,
                        ""nBest"": [
                            {
                                ""display"": ""Hello from file one""
                            }
                        ]
                    },
                    {
                        ""channel"": 0,
                        ""speaker"": 1,
                        ""offsetInTicks"": 20000000,
                        ""durationInTicks"": 10000000,
                        ""nBest"": [
                            {
                                ""display"": ""This is still file one""
                            }
                        ]
                    },
                    {
                        ""channel"": 1,
                        ""speaker"": 2,
                        ""offsetInTicks"": 0,
                        ""durationInTicks"": 10000000,
                        ""nBest"": [
                            {
                                ""display"": ""Hello from file two""
                            }
                        ]
                    },
                    {
                        ""channel"": 1,
                        ""speaker"": 2,
                        ""offsetInTicks"": 20000000,
                        ""durationInTicks"": 10000000,
                        ""nBest"": [
                            {
                                ""display"": ""This is still file two""
                            }
                        ]
                    }
                ]
            }";
        }

        private string CreateSingleFileTranscriptionJson()
        {
            return @"{
                ""recognizedPhrases"": [
                    {
                        ""channel"": 0,
                        ""speaker"": 1,
                        ""offsetInTicks"": 0,
                        ""durationInTicks"": 10000000,
                        ""nBest"": [
                            {
                                ""display"": ""Hello from single file""
                            }
                        ]
                    }
                ]
            }";
        }

        /// <summary>
        /// Creates an HttpClientFactory mock that properly handles multiple requests.
        /// Uses a thread-safe counter to track request sequence across multiple HttpClient instances.
        /// This is the corrected version that works.
        /// </summary>
        private Mock<IHttpClientFactory> CreateMockHttpClientFactory(string jobJson, string filesJson, string transcriptionJson)
        {
            var mockFactory = new Mock<IHttpClientFactory>();
            var requestCounter = 0;
            var lockObj = new object();

            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() =>
                {
                    var mockHandler = new Mock<HttpMessageHandler>();
                    mockHandler.Protected()
                        .Setup<Task<HttpResponseMessage>>(
                            "SendAsync",
                            ItExpr.IsAny<HttpRequestMessage>(),
                            ItExpr.IsAny<CancellationToken>())
                        .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
                        {
                            int counter;
                            lock (lockObj)
                            {
                                counter = requestCounter++;
                            }

                            return counter switch
                            {
                                0 => new HttpResponseMessage
                                {
                                    StatusCode = HttpStatusCode.OK,
                                    Content = new StringContent(jobJson, Encoding.UTF8, "application/json")
                                },
                                1 => new HttpResponseMessage
                                {
                                    StatusCode = HttpStatusCode.OK,
                                    Content = new StringContent(filesJson, Encoding.UTF8, "application/json")
                                },
                                2 => new HttpResponseMessage
                                {
                                    StatusCode = HttpStatusCode.OK,
                                    Content = new StringContent(transcriptionJson, Encoding.UTF8, "application/json")
                                },
                                _ => new HttpResponseMessage
                                {
                                    StatusCode = HttpStatusCode.InternalServerError,
                                    Content = new StringContent($"Too many requests in test (request #{counter})", Encoding.UTF8)
                                }
                            };
                        });

                    return new HttpClient(mockHandler.Object);
                });

            return mockFactory;
        }

        /// <summary>
        /// OLD METHOD - kept for compatibility but should not be used
        /// </summary>
        [Obsolete("Use CreateMockHttpClientFactory instead")]
        private Mock<IHttpClientFactory> SetupHttpClientFactory(string jobJson, string filesJson, string transcriptionJson)
        {
            return CreateMockHttpClientFactory(jobJson, filesJson, transcriptionJson);
        }

        /// <summary>
        /// Creates an HttpClientFactory mock for single request scenarios
        /// </summary>
        private Mock<IHttpClientFactory> SetupHttpClientFactoryForSingleRequest(string responseJson)
        {
            var mockFactory = new Mock<IHttpClientFactory>();
            var mockHandler = new Mock<HttpMessageHandler>();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(mockHandler.Object));

            return mockFactory;
        }

        /// <summary>
        /// Creates an HttpClientFactory mock that returns 404 Not Found
        /// </summary>
        private Mock<IHttpClientFactory> SetupHttpClientFactoryForNotFound()
        {
            var mockFactory = new Mock<IHttpClientFactory>();
            var mockHandler = new Mock<HttpMessageHandler>();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            };

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(mockHandler.Object));

            return mockFactory;
        }

        private TranscriptionJobService CreateService(Mock<IHttpClientFactory> mockFactory)
        {
            return new TranscriptionJobService(
                _mockConfiguration.Object,
                _mockLogger.Object,
                mockFactory.Object);
        }
    }
}
