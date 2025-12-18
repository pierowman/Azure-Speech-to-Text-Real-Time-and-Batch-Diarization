using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using speechtotext.Models;
using speechtotext.Services;
using System.Net;
using Xunit;

namespace speechtotext.Tests.Services
{
    public class BatchTranscriptionServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<BatchTranscriptionService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IOptions<AzureStorageOptions>> _mockStorageOptions;
        private readonly Mock<IOptions<AudioUploadOptions>> _mockAudioUploadOptions;

        public BatchTranscriptionServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<BatchTranscriptionService>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockStorageOptions = new Mock<IOptions<AzureStorageOptions>>();
            _mockAudioUploadOptions = new Mock<IOptions<AudioUploadOptions>>();

            // Setup configuration
            _mockConfiguration.Setup(c => c["AzureSpeech:SubscriptionKey"]).Returns("test-key");
            _mockConfiguration.Setup(c => c["AzureSpeech:Region"]).Returns("eastus");
            
            // Setup HttpClientFactory to return a valid HttpClient
            var httpClient = new HttpClient();
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            
            // Setup storage options (disabled by default for tests) - Azure AD configuration
            var storageOptions = new AzureStorageOptions
            {
                EnableBlobStorage = false,
                StorageAccountName = "teststorage",
                ContainerName = "audio-uploads",
                TenantId = "test-tenant-id",
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret",
                UseManagedIdentity = false
            };
            _mockStorageOptions.Setup(o => o.Value).Returns(storageOptions);
            
            // Setup audio upload options with default speaker settings
            var audioUploadOptions = new AudioUploadOptions
            {
                DefaultMinSpeakers = 2,
                DefaultMaxSpeakers = 3
            };
            _mockAudioUploadOptions.Setup(o => o.Value).Returns(audioUploadOptions);
        }

        [Fact]
        public void BatchTranscriptionService_Constructor_ShouldThrowException_WhenSubscriptionKeyMissing()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["AzureSpeech:SubscriptionKey"]).Returns((string?)null);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new BatchTranscriptionService(_mockConfiguration.Object, _mockLogger.Object, _mockHttpClientFactory.Object, _mockStorageOptions.Object, _mockAudioUploadOptions.Object));

            Assert.Contains("subscription key not found", exception.Message);
        }

        [Fact]
        public void BatchTranscriptionService_Constructor_ShouldThrowException_WhenRegionMissing()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["AzureSpeech:Region"]).Returns((string?)null);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new BatchTranscriptionService(_mockConfiguration.Object, _mockLogger.Object, _mockHttpClientFactory.Object, _mockStorageOptions.Object, _mockAudioUploadOptions.Object));

            Assert.Contains("region not found", exception.Message);
        }

        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldReturnPlaceholderJob_WhenBlobStorageNotConfigured()
        {
            // Arrange
            // Blob storage is disabled by default in constructor
            var service = CreateService();
            var filePaths = new[] { "file1.wav", "file2.mp3" };
            var jobName = "Test Job";

            // Act
            var result = await service.CreateBatchTranscriptionAsync(filePaths, jobName);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(string.Empty, result.Id);
            Assert.Equal(jobName, result.DisplayName);
            Assert.Equal("NotStarted", result.Status);
            Assert.Equal(2, result.Files.Count);
            Assert.NotNull(result.Error);
            Assert.Contains("not configured", result.Error);
        }

        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldReturnPlaceholderJob_WhenApiFails()
        {
            // Arrange
            SetupHttpClient(HttpStatusCode.InternalServerError, "{}");
            var service = CreateService();
            var filePaths = new[] { "file1.wav" };
            var jobName = "Test Job";

            // Act
            var result = await service.CreateBatchTranscriptionAsync(filePaths, jobName);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(string.Empty, result.Id);
            Assert.Equal(jobName, result.DisplayName);
            Assert.Equal("NotStarted", result.Status);
        }

        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldIncludeFilenames()
        {
            // Arrange - Blob storage disabled, will return placeholder
            var service = CreateService();
            var filePaths = new[] { @"C:\path\to\file1.wav", @"C:\path\to\file2.mp3" };
            var jobName = "Multi-File Job";

            // Act
            var result = await service.CreateBatchTranscriptionAsync(filePaths, jobName);

            // Assert
            Assert.Equal(2, result.Files.Count);
            Assert.Contains("file1.wav", result.Files);
            Assert.Contains("file2.mp3", result.Files);
        }

        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldUseCustomLanguage()
        {
            // Arrange - Blob storage disabled, will return placeholder
            var service = CreateService();
            var filePaths = new[] { "file1.wav" };
            var jobName = "Spanish Job";
            var language = "es-ES";

            // Act
            var result = await service.CreateBatchTranscriptionAsync(filePaths, jobName, language);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(jobName, result.DisplayName);
        }

        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldHandleDiarizationSettings()
        {
            // Arrange - Blob storage disabled, will return placeholder
            var service = CreateService();
            var filePaths = new[] { "file1.wav" };
            var jobName = "No Diarization Job";

            // Act
            var result = await service.CreateBatchTranscriptionAsync(filePaths, jobName, "en-US", false);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldLogInformation()
        {
            // Arrange - Blob storage disabled, will return placeholder
            var service = CreateService();
            var filePaths = new[] { "file1.wav" };
            var jobName = "Logging Test";

            // Act
            await service.CreateBatchTranscriptionAsync(filePaths, jobName);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating batch transcription")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldHandleHttpException()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var httpClient = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var service = CreateService();
            var filePaths = new[] { "file1.wav" };

            // Act
            var result = await service.CreateBatchTranscriptionAsync(filePaths, "Error Test");

            // Assert - Should return placeholder job even with exception
            Assert.NotNull(result);
            Assert.Equal("NotStarted", result.Status);
        }

        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldSupportCancellation()
        {
            // Arrange - Blob storage disabled, will return placeholder
            var service = CreateService();
            var filePaths = new[] { "file1.wav" };
            var cts = new CancellationTokenSource();

            // Act
            var result = await service.CreateBatchTranscriptionAsync(filePaths, "Cancel Test", cancellationToken: cts.Token);

            // Assert
            Assert.NotNull(result);
        }
        
        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldUseDefaultSpeakers_WhenNotSpecified()
        {
            // Arrange
            var service = CreateService();
            var filePaths = new[] { "file1.wav" };
            var jobName = "Default Speakers Test";

            // Act - Not specifying speaker parameters
            var result = await service.CreateBatchTranscriptionAsync(filePaths, jobName);

            // Assert
            Assert.NotNull(result);
            // Verify that the logger was called with the default speaker counts (2-3)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("2-3")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        
        [Fact]
        public async Task CreateBatchTranscriptionAsync_ShouldUseCustomSpeakers_WhenSpecified()
        {
            // Arrange
            var service = CreateService();
            var filePaths = new[] { "file1.wav" };
            var jobName = "Custom Speakers Test";

            // Act - Specifying custom speaker parameters
            var result = await service.CreateBatchTranscriptionAsync(
                filePaths, 
                jobName, 
                "en-US", 
                true, 
                minSpeakers: 1, 
                maxSpeakers: 5);

            // Assert
            Assert.NotNull(result);
            // Verify that the logger was called with the custom speaker counts (1-5)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("1-5")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetSupportedLocalesAsync_ShouldReturnLocales_WhenApiSucceeds()
        {
            // Arrange
            var localesJson = @"{
                ""en-US"": { ""name"": ""English (United States)"" },
                ""es-ES"": { ""name"": ""Spanish (Spain)"" },
                ""fr-FR"": { ""name"": ""French (France)"" },
                ""de-DE"": { ""name"": ""German (Germany)"" }
            }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesAsync();

            // Assert
            Assert.NotNull(result);
            var localesList = result.ToList();
            Assert.NotEmpty(localesList);
            Assert.Contains("en-US", localesList);
            Assert.Contains("es-ES", localesList);
            Assert.Contains("fr-FR", localesList);
            Assert.Contains("de-DE", localesList);
        }

        [Fact]
        public async Task GetSupportedLocalesAsync_ShouldReturnFallbackLocales_WhenApiFails()
        {
            // Arrange
            SetupHttpClient(HttpStatusCode.InternalServerError, "");
            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesAsync();

            // Assert
            Assert.NotNull(result);
            var localesList = result.ToList();
            Assert.NotEmpty(localesList);
            // Should contain common fallback locales
            Assert.Contains("en-US", localesList);
            Assert.Contains("es-ES", localesList);
            Assert.Contains("fr-FR", localesList);
        }

        [Fact]
        public async Task GetSupportedLocalesAsync_ShouldReturnFallbackLocales_OnException()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var httpClient = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesAsync();

            // Assert
            Assert.NotNull(result);
            var localesList = result.ToList();
            Assert.NotEmpty(localesList);
            // Should return fallback list
            Assert.Contains("en-US", localesList);
        }

        [Fact]
        public async Task GetSupportedLocalesAsync_ShouldSortLocalesAlphabetically()
        {
            // Arrange
            var localesJson = @"{
                ""zh-CN"": {},
                ""en-US"": {},
                ""ar-SA"": {},
                ""fr-FR"": {}
            }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesAsync();

            // Assert
            var localesList = result.ToList();
            Assert.NotEmpty(localesList);
            // Verify sorted order
            Assert.Equal("ar-SA", localesList[0]);
            Assert.Equal("en-US", localesList[1]);
            Assert.Equal("fr-FR", localesList[2]);
            Assert.Equal("zh-CN", localesList[3]);
        }

        [Fact]
        public async Task GetSupportedLocalesAsync_ShouldLogInformation()
        {
            // Arrange
            var localesJson = @"{ ""en-US"": {}, ""es-ES"": {} }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();

            // Act
            await service.GetSupportedLocalesAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fetching supported locales")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetSupportedLocalesAsync_ShouldSupportCancellation()
        {
            // Arrange
            var localesJson = @"{ ""en-US"": {} }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();
            var cts = new CancellationTokenSource();

            // Act
            var result = await service.GetSupportedLocalesAsync(cts.Token);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetSupportedLocalesWithNamesAsync_ShouldReturnLocalesWithNames_WhenApiSucceeds()
        {
            // Arrange
            var localesJson = @"{
                ""en-US"": { ""name"": ""English (United States)"" },
                ""es-ES"": { ""name"": ""Spanish (Spain)"" },
                ""fr-FR"": { ""name"": ""French (France)"" },
                ""de-DE"": { ""name"": ""German (Germany)"" }
            }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesWithNamesAsync();

            // Assert
            Assert.NotNull(result);
            var localesList = result.ToList();
            Assert.NotEmpty(localesList);
            Assert.Equal(4, localesList.Count);
            
            // Verify specific locales (no emojis)
            var enUS = localesList.FirstOrDefault(l => l.Code == "en-US");
            Assert.NotNull(enUS);
            Assert.Equal("English (United States)", enUS.Name);
            Assert.Equal("English (United States)", enUS.FormattedName); // No emoji
            
            var esES = localesList.FirstOrDefault(l => l.Code == "es-ES");
            Assert.NotNull(esES);
            Assert.Equal("Spanish (Spain)", esES.Name);
            Assert.Equal("Spanish (Spain)", esES.FormattedName); // No emoji
        }

        [Fact]
        public async Task GetSupportedLocalesWithNamesAsync_ShouldReturnFallbackLocalesWithNames_WhenApiFails()
        {
            // Arrange
            SetupHttpClient(HttpStatusCode.InternalServerError, "");
            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesWithNamesAsync();

            // Assert
            Assert.NotNull(result);
            var localesList = result.ToList();
            Assert.NotEmpty(localesList);
            
            // Should contain common fallback locales with names
            var enUS = localesList.FirstOrDefault(l => l.Code == "en-US");
            Assert.NotNull(enUS);
            Assert.Equal("English (United States)", enUS.Name);
        }

        [Fact]
        public async Task GetSupportedLocalesWithNamesAsync_ShouldHandleLocalesWithoutNames()
        {
            // Arrange
            var localesJson = @"{
                ""en-US"": { ""name"": ""English (United States)"" },
                ""xx-YY"": {}
            }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesWithNamesAsync();

            // Assert
            var localesList = result.ToList();
            Assert.Equal(2, localesList.Count);
            
            // Locale without name should default to code in uppercase
            var xxYY = localesList.FirstOrDefault(l => l.Code == "xx-YY");
            Assert.NotNull(xxYY);
            Assert.Equal("XX-YY", xxYY.Name);
        }

        [Fact]
        public async Task GetSupportedLocalesWithNamesAsync_ShouldSortByCode()
        {
            // Arrange
            var localesJson = @"{
                ""zh-CN"": { ""name"": ""Chinese (Simplified)"" },
                ""en-US"": { ""name"": ""English (United States)"" },
                ""ar-SA"": { ""name"": ""Arabic (Saudi Arabia)"" },
                ""fr-FR"": { ""name"": ""French (France)"" }
            }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesWithNamesAsync();

            // Assert
            var localesList = result.ToList();
            Assert.NotEmpty(localesList);
            // Verify sorted order by code
            Assert.Equal("ar-SA", localesList[0].Code);
            Assert.Equal("en-US", localesList[1].Code);
            Assert.Equal("fr-FR", localesList[2].Code);
            Assert.Equal("zh-CN", localesList[3].Code);
        }

        [Fact]
        public async Task GetSupportedLocalesWithNamesAsync_ShouldReturnFallbackOnException()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var httpClient = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var service = CreateService();

            // Act
            var result = await service.GetSupportedLocalesWithNamesAsync();

            // Assert
            Assert.NotNull(result);
            var localesList = result.ToList();
            Assert.NotEmpty(localesList);
            // Should return fallback list
            Assert.Contains(localesList, l => l.Code == "en-US");
        }

        [Fact]
        public async Task GetSupportedLocalesWithNamesAsync_ShouldLogInformation()
        {
            // Arrange
            var localesJson = @"{ ""en-US"": { ""name"": ""English (United States)"" } }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();

            // Act
            await service.GetSupportedLocalesWithNamesAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fetching supported locales with names")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetSupportedLocalesWithNamesAsync_ShouldSupportCancellation()
        {
            // Arrange
            var localesJson = @"{ ""en-US"": { ""name"": ""English (United States)"" } }";
            SetupHttpClient(HttpStatusCode.OK, localesJson);
            var service = CreateService();
            var cts = new CancellationTokenSource();

            // Act
            var result = await service.GetSupportedLocalesWithNamesAsync(cts.Token);

            // Assert
            Assert.NotNull(result);
        }

        private void SetupHttpClient(HttpStatusCode statusCode, string responseContent)
        {
            var response = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
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

        private BatchTranscriptionService CreateService()
        {
            return new BatchTranscriptionService(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _mockHttpClientFactory.Object,
                _mockStorageOptions.Object,
                _mockAudioUploadOptions.Object);
        }
    }
}
