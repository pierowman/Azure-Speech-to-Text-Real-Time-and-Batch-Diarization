using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models
{
    public class BatchTranscriptionModelsTests
    {
        [Fact]
        public void BatchTranscriptionRequest_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var request = new BatchTranscriptionRequest();

            // Assert
            Assert.Null(request.JobName);
            Assert.Equal("en-US", request.Language);
            Assert.True(request.EnableDiarization);
        }

        [Fact]
        public void BatchTranscriptionRequest_ShouldAllowSettingProperties()
        {
            // Arrange
            var jobName = "Test Job";
            var language = "es-ES";
            var enableDiarization = false;

            // Act
            var request = new BatchTranscriptionRequest
            {
                JobName = jobName,
                Language = language,
                EnableDiarization = enableDiarization
            };

            // Assert
            Assert.Equal(jobName, request.JobName);
            Assert.Equal(language, request.Language);
            Assert.Equal(enableDiarization, request.EnableDiarization);
        }

        [Fact]
        public void BatchTranscriptionResponse_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var response = new BatchTranscriptionResponse();

            // Assert
            Assert.False(response.Success);
            Assert.Equal(string.Empty, response.Message);
            Assert.Null(response.JobId);
            Assert.Null(response.JobName);
            Assert.Equal(0, response.FilesSubmitted);
        }

        [Fact]
        public void BatchTranscriptionResponse_ShouldAllowSettingProperties()
        {
            // Arrange
            var success = true;
            var message = "Job submitted";
            var jobId = "job-123";
            var jobName = "My Job";
            var filesSubmitted = 5;

            // Act
            var response = new BatchTranscriptionResponse
            {
                Success = success,
                Message = message,
                JobId = jobId,
                JobName = jobName,
                FilesSubmitted = filesSubmitted
            };

            // Assert
            Assert.True(response.Success);
            Assert.Equal(message, response.Message);
            Assert.Equal(jobId, response.JobId);
            Assert.Equal(jobName, response.JobName);
            Assert.Equal(filesSubmitted, response.FilesSubmitted);
        }
    }
}
