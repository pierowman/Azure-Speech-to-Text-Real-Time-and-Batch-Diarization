using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models
{
    public class TranscriptionJobTests
    {
        [Fact]
        public void TranscriptionJob_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var job = new TranscriptionJob();

            // Assert
            Assert.Equal(string.Empty, job.Id);
            Assert.Equal(string.Empty, job.DisplayName);
            Assert.Equal(string.Empty, job.Status);
            Assert.Equal(default(DateTime), job.CreatedDateTime);
            Assert.Null(job.LastActionDateTime);
            Assert.Null(job.Error);
            Assert.NotNull(job.Files);
            Assert.Empty(job.Files);
            Assert.Null(job.ResultsUrl);
            Assert.Null(job.Properties);
            Assert.Null(job.Locale);
        }

        [Fact]
        public void TranscriptionJob_ShouldSetProperties()
        {
            // Arrange
            var jobId = "test-job-123";
            var displayName = "Test Transcription";
            var status = "Running";
            var createdDate = DateTime.UtcNow;
            var lastActionDate = DateTime.UtcNow.AddMinutes(5);
            var error = "Test error message";
            var files = new List<string> { "file1.wav", "file2.mp3" };
            var resultsUrl = "https://example.com/results";
            var locale = "en-US";

            // Act
            var job = new TranscriptionJob
            {
                Id = jobId,
                DisplayName = displayName,
                Status = status,
                CreatedDateTime = createdDate,
                LastActionDateTime = lastActionDate,
                Error = error,
                Files = files,
                ResultsUrl = resultsUrl,
                Locale = locale
            };

            // Assert
            Assert.Equal(jobId, job.Id);
            Assert.Equal(displayName, job.DisplayName);
            Assert.Equal(status, job.Status);
            Assert.Equal(createdDate, job.CreatedDateTime);
            Assert.Equal(lastActionDate, job.LastActionDateTime);
            Assert.Equal(error, job.Error);
            Assert.Equal(files, job.Files);
            Assert.Equal(resultsUrl, job.ResultsUrl);
            Assert.Equal(locale, job.Locale);
        }

        [Fact]
        public void TranscriptionJob_FilesProperty_ShouldAllowMultipleFiles()
        {
            // Arrange
            var job = new TranscriptionJob();
            var files = new List<string>
            {
                "audio1.wav",
                "audio2.mp3",
                "audio3.flac"
            };

            // Act
            job.Files = files;

            // Assert
            Assert.Equal(3, job.Files.Count);
            Assert.Contains("audio1.wav", job.Files);
            Assert.Contains("audio2.mp3", job.Files);
            Assert.Contains("audio3.flac", job.Files);
        }

        [Fact]
        public void TranscriptionProperties_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var props = new TranscriptionProperties();

            // Assert
            Assert.Null(props.Duration);
            Assert.Null(props.SucceededCount);
            Assert.Null(props.FailedCount);
            Assert.Null(props.ErrorMessage);
        }

        [Fact]
        public void TranscriptionProperties_ShouldSetProperties()
        {
            // Arrange
            var duration = 12345;
            var succeededCount = 5;
            var failedCount = 2;
            var errorMessage = "Some files failed";

            // Act
            var props = new TranscriptionProperties
            {
                Duration = duration,
                SucceededCount = succeededCount,
                FailedCount = failedCount,
                ErrorMessage = errorMessage
            };

            // Assert
            Assert.Equal(duration, props.Duration);
            Assert.Equal(succeededCount, props.SucceededCount);
            Assert.Equal(failedCount, props.FailedCount);
            Assert.Equal(errorMessage, props.ErrorMessage);
        }

        [Fact]
        public void CancelJobRequest_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var request = new CancelJobRequest();

            // Assert
            Assert.Equal(string.Empty, request.JobId);
        }

        [Fact]
        public void CancelJobRequest_ShouldSetJobId()
        {
            // Arrange
            var jobId = "job-to-cancel-123";

            // Act
            var request = new CancelJobRequest { JobId = jobId };

            // Assert
            Assert.Equal(jobId, request.JobId);
        }

        [Fact]
        public void JobOperationResponse_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var response = new JobOperationResponse();

            // Assert
            Assert.False(response.Success);
            Assert.Equal(string.Empty, response.Message);
            Assert.Null(response.Job);
        }

        [Fact]
        public void JobOperationResponse_ShouldSetProperties()
        {
            // Arrange
            var success = true;
            var message = "Operation completed successfully";
            var job = new TranscriptionJob { Id = "test-123" };

            // Act
            var response = new JobOperationResponse
            {
                Success = success,
                Message = message,
                Job = job
            };

            // Assert
            Assert.True(response.Success);
            Assert.Equal(message, response.Message);
            Assert.NotNull(response.Job);
            Assert.Equal("test-123", response.Job.Id);
        }

        [Theory]
        [InlineData("Succeeded")]
        [InlineData("Running")]
        [InlineData("Failed")]
        [InlineData("NotStarted")]
        public void TranscriptionJob_ShouldHandleVariousStatuses(string status)
        {
            // Arrange & Act
            var job = new TranscriptionJob { Status = status };

            // Assert
            Assert.Equal(status, job.Status);
        }

        [Fact]
        public void TranscriptionJob_WithProperties_ShouldLinkCorrectly()
        {
            // Arrange
            var props = new TranscriptionProperties
            {
                Duration = 5000,
                SucceededCount = 3,
                FailedCount = 1,
                ErrorMessage = "One file had issues"
            };

            // Act
            var job = new TranscriptionJob
            {
                Id = "test-job",
                Properties = props
            };

            // Assert
            Assert.NotNull(job.Properties);
            Assert.Equal(5000, job.Properties.Duration);
            Assert.Equal(3, job.Properties.SucceededCount);
            Assert.Equal(1, job.Properties.FailedCount);
            Assert.Equal("One file had issues", job.Properties.ErrorMessage);
        }

        #region Locale Tests

        [Theory]
        [InlineData("en-US")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("de-DE")]
        [InlineData("ja-JP")]
        public void Locale_ShouldStoreLocaleCode(string locale)
        {
            // Arrange & Act
            var job = new TranscriptionJob { Locale = locale };

            // Assert
            Assert.Equal(locale, job.Locale);
        }

        [Fact]
        public void Locale_WhenNull_ShouldAllowNull()
        {
            // Arrange & Act
            var job = new TranscriptionJob { Locale = null };

            // Assert
            Assert.Null(job.Locale);
        }

        [Fact]
        public void Locale_WhenEmpty_ShouldAllowEmpty()
        {
            // Arrange & Act
            var job = new TranscriptionJob { Locale = "" };

            // Assert
            Assert.Equal("", job.Locale);
        }

        [Fact]
        public void Locale_WhenUnknownCode_ShouldStoreAsIs()
        {
            // Arrange
            var unknownLocale = "xx-YY";
            
            // Act
            var job = new TranscriptionJob { Locale = unknownLocale };

            // Assert
            Assert.Equal(unknownLocale, job.Locale);
        }

        #endregion

        #region FormattedDuration Tests

        [Fact]
        public void FormattedDuration_WhenPropertiesIsNull_ShouldReturnNA()
        {
            // Arrange
            var job = new TranscriptionJob { Properties = null };

            // Act
            var formattedDuration = job.FormattedDuration;

            // Assert
            Assert.Equal("N/A", formattedDuration);
        }

        [Fact]
        public void FormattedDuration_WhenDurationIsNull_ShouldReturnNA()
        {
            // Arrange
            var job = new TranscriptionJob
            {
                Properties = new TranscriptionProperties { Duration = null }
            };

            // Act
            var formattedDuration = job.FormattedDuration;

            // Assert
            Assert.Equal("N/A", formattedDuration);
        }

        [Fact]
        public void FormattedDuration_WhenDurationIsZero_ShouldReturnNA()
        {
            // Arrange
            var job = new TranscriptionJob
            {
                Properties = new TranscriptionProperties { Duration = 0 }
            };

            // Act
            var formattedDuration = job.FormattedDuration;

            // Assert
            Assert.Equal("N/A", formattedDuration);
        }

        [Theory]
        [InlineData(10000000, "00:00:01")]      // 1 second (10,000,000 ticks)
        [InlineData(600000000, "00:01:00")]     // 1 minute (600,000,000 ticks)
        [InlineData(36000000000, "01:00:00")]   // 1 hour (36,000,000,000 ticks)
        [InlineData(37500000000, "01:02:30")]   // 1 hour 2 min 30 sec
        [InlineData(50000000, "00:00:05")]      // 5 seconds
        [InlineData(125000000, "00:00:12")]     // 12.5 seconds (rounds down)
        public void FormattedDuration_ShouldFormatTicksCorrectly(long ticks, string expectedFormat)
        {
            // Arrange
            var job = new TranscriptionJob
            {
                Properties = new TranscriptionProperties { Duration = ticks }
            };

            // Act
            var formattedDuration = job.FormattedDuration;

            // Assert
            Assert.Equal(expectedFormat, formattedDuration);
        }

        #endregion

        #region TotalFileCount Tests

        [Fact]
        public void TotalFileCount_WhenFilesIsNull_ShouldReturnZero()
        {
            // Arrange
            var job = new TranscriptionJob { Files = null! };

            // Act
            var count = job.TotalFileCount;

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void TotalFileCount_WhenFilesIsEmpty_ShouldReturnZero()
        {
            // Arrange
            var job = new TranscriptionJob { Files = new List<string>() };

            // Act
            var count = job.TotalFileCount;

            // Assert
            Assert.Equal(0, count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        public void TotalFileCount_ShouldReturnCorrectCount(int fileCount)
        {
            // Arrange
            var files = Enumerable.Range(1, fileCount)
                .Select(i => $"file{i}.wav")
                .ToList();
            var job = new TranscriptionJob { Files = files };

            // Act
            var count = job.TotalFileCount;

            // Assert
            Assert.Equal(fileCount, count);
        }

        #endregion
    }
}
