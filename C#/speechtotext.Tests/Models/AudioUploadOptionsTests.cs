using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models
{
    public class AudioUploadOptionsTests
    {
        [Fact]
        public void AudioUploadOptions_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var options = new AudioUploadOptions();

            // Assert
            // Real-Time defaults
            Assert.NotNull(options.RealTimeAllowedExtensions);
            Assert.Single(options.RealTimeAllowedExtensions);
            Assert.Contains(".wav", options.RealTimeAllowedExtensions);
            Assert.Equal(25 * 1024 * 1024, options.RealTimeMaxFileSizeInBytes);
            Assert.Equal(60, options.RealTimeMaxDurationInMinutes);
            
            // Batch defaults
            Assert.NotNull(options.BatchAllowedExtensions);
            Assert.Equal(4, options.BatchAllowedExtensions.Length);
            Assert.Contains(".wav", options.BatchAllowedExtensions);
            Assert.Contains(".mp3", options.BatchAllowedExtensions);
            Assert.Contains(".flac", options.BatchAllowedExtensions);
            Assert.Contains(".ogg", options.BatchAllowedExtensions);
            Assert.Equal(1024 * 1024 * 1024, options.BatchMaxFileSizeInBytes);
            Assert.Equal(480, options.BatchMaxDurationInMinutes);
            Assert.Equal(20, options.BatchMaxFiles);
            
            // Speaker diarization defaults
            Assert.Equal(2, options.DefaultMinSpeakers);
            Assert.Equal(3, options.DefaultMaxSpeakers);
            
            // Locale defaults
            Assert.Equal("en-US", options.DefaultLocale);
            
            // Common defaults
            Assert.Equal("uploads", options.UploadFolderPath);
            Assert.True(options.ShowTranscriptionJobsTab);
        }

        [Fact]
        public void AudioUploadOptions_ShowTranscriptionJobsTab_ShouldDefaultToTrue()
        {
            // Arrange & Act
            var options = new AudioUploadOptions();

            // Assert
            Assert.True(options.ShowTranscriptionJobsTab);
        }

        [Fact]
        public void AudioUploadOptions_ShouldAllowSettingShowTranscriptionJobsTab_ToFalse()
        {
            // Arrange
            var options = new AudioUploadOptions
            {
                ShowTranscriptionJobsTab = false
            };

            // Act & Assert
            Assert.False(options.ShowTranscriptionJobsTab);
        }

        [Fact]
        public void AudioUploadOptions_ShouldAllowSettingRealTimeProperties()
        {
            // Arrange
            var customExtensions = new[] { ".wav", ".mp3" };
            var customMaxSize = 50 * 1024 * 1024; // 50 MB
            var customMaxDuration = 90;

            // Act
            var options = new AudioUploadOptions
            {
                RealTimeAllowedExtensions = customExtensions,
                RealTimeMaxFileSizeInBytes = customMaxSize,
                RealTimeMaxDurationInMinutes = customMaxDuration
            };

            // Assert
            Assert.Equal(customExtensions, options.RealTimeAllowedExtensions);
            Assert.Equal(customMaxSize, options.RealTimeMaxFileSizeInBytes);
            Assert.Equal(customMaxDuration, options.RealTimeMaxDurationInMinutes);
        }

        [Fact]
        public void AudioUploadOptions_ShouldAllowSettingBatchProperties()
        {
            // Arrange
            var customExtensions = new[] { ".wav", ".mp3", ".flac" };
            var customMaxSize = 2L * 1024 * 1024 * 1024; // 2 GB
            var customMaxDuration = 600;
            var customMaxFiles = 50;

            // Act
            var options = new AudioUploadOptions
            {
                BatchAllowedExtensions = customExtensions,
                BatchMaxFileSizeInBytes = customMaxSize,
                BatchMaxDurationInMinutes = customMaxDuration,
                BatchMaxFiles = customMaxFiles
            };

            // Assert
            Assert.Equal(customExtensions, options.BatchAllowedExtensions);
            Assert.Equal(customMaxSize, options.BatchMaxFileSizeInBytes);
            Assert.Equal(customMaxDuration, options.BatchMaxDurationInMinutes);
            Assert.Equal(customMaxFiles, options.BatchMaxFiles);
        }

        [Fact]
        public void AudioUploadOptions_BackwardCompatibility_AllowedExtensions_ShouldReturnRealTimeExtensions()
        {
            // Arrange
            var options = new AudioUploadOptions();

            // Act
            #pragma warning disable CS0618 // Type or member is obsolete
            var extensions = options.AllowedExtensions;
            #pragma warning restore CS0618

            // Assert
            Assert.Equal(options.RealTimeAllowedExtensions, extensions);
        }

        [Fact]
        public void AudioUploadOptions_BackwardCompatibility_MaxFileSizeInBytes_ShouldReturnRealTimeMaxSize()
        {
            // Arrange
            var options = new AudioUploadOptions();

            // Act
            #pragma warning disable CS0618 // Type or member is obsolete
            var maxSize = options.MaxFileSizeInBytes;
            #pragma warning restore CS0618

            // Assert
            Assert.Equal(options.RealTimeMaxFileSizeInBytes, maxSize);
        }

        [Fact]
        public void AudioUploadOptions_BackwardCompatibility_MaxDurationInMinutes_ShouldReturnRealTimeMaxDuration()
        {
            // Arrange
            var options = new AudioUploadOptions();

            // Act
            #pragma warning disable CS0618 // Type or member is obsolete
            var maxDuration = options.MaxDurationInMinutes;
            #pragma warning restore CS0618

            // Assert
            Assert.Equal(options.RealTimeMaxDurationInMinutes, maxDuration);
        }

        [Fact]
        public void AudioUploadOptions_ShouldAllowOverridingAllProperties()
        {
            // Arrange
            var rtExtensions = new[] { ".wav" };
            var rtMaxSize = 30 * 1024 * 1024;
            var rtMaxDuration = 45;
            var batchExtensions = new[] { ".wav", ".mp3" };
            var batchMaxSize = 512 * 1024 * 1024;
            var batchMaxDuration = 240;
            var batchMaxFiles = 10;
            var uploadPath = "custom-uploads";
            var showJobsTab = false;

            // Act
            var options = new AudioUploadOptions
            {
                RealTimeAllowedExtensions = rtExtensions,
                RealTimeMaxFileSizeInBytes = rtMaxSize,
                RealTimeMaxDurationInMinutes = rtMaxDuration,
                BatchAllowedExtensions = batchExtensions,
                BatchMaxFileSizeInBytes = batchMaxSize,
                BatchMaxDurationInMinutes = batchMaxDuration,
                BatchMaxFiles = batchMaxFiles,
                UploadFolderPath = uploadPath,
                ShowTranscriptionJobsTab = showJobsTab
            };

            // Assert
            Assert.Equal(rtExtensions, options.RealTimeAllowedExtensions);
            Assert.Equal(rtMaxSize, options.RealTimeMaxFileSizeInBytes);
            Assert.Equal(rtMaxDuration, options.RealTimeMaxDurationInMinutes);
            Assert.Equal(batchExtensions, options.BatchAllowedExtensions);
            Assert.Equal(batchMaxSize, options.BatchMaxFileSizeInBytes);
            Assert.Equal(batchMaxDuration, options.BatchMaxDurationInMinutes);
            Assert.Equal(batchMaxFiles, options.BatchMaxFiles);
            Assert.Equal(uploadPath, options.UploadFolderPath);
            Assert.Equal(showJobsTab, options.ShowTranscriptionJobsTab);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AudioUploadOptions_ShouldRespectShowTranscriptionJobsTabValue(bool value)
        {
            // Arrange
            var options = new AudioUploadOptions
            {
                ShowTranscriptionJobsTab = value
            };

            // Act & Assert
            Assert.Equal(value, options.ShowTranscriptionJobsTab);
        }

        [Fact]
        public void AudioUploadOptions_DefaultMinSpeakers_ShouldBeTwo()
        {
            // Arrange & Act
            var options = new AudioUploadOptions();

            // Assert
            Assert.Equal(2, options.DefaultMinSpeakers);
        }

        [Fact]
        public void AudioUploadOptions_DefaultMaxSpeakers_ShouldBeThree()
        {
            // Arrange & Act
            var options = new AudioUploadOptions();

            // Assert
            Assert.Equal(3, options.DefaultMaxSpeakers);
        }

        [Fact]
        public void AudioUploadOptions_ShouldAllowCustomSpeakerSettings()
        {
            // Arrange
            var customMinSpeakers = 1;
            var customMaxSpeakers = 10;

            // Act
            var options = new AudioUploadOptions
            {
                DefaultMinSpeakers = customMinSpeakers,
                DefaultMaxSpeakers = customMaxSpeakers
            };

            // Assert
            Assert.Equal(customMinSpeakers, options.DefaultMinSpeakers);
            Assert.Equal(customMaxSpeakers, options.DefaultMaxSpeakers);
        }

        [Theory]
        [InlineData(1, 1)]  // Single speaker
        [InlineData(2, 2)]  // Exact two speakers (interview)
        [InlineData(2, 3)]  // Small meeting
        [InlineData(1, 5)]  // Medium meeting
        [InlineData(1, 10)] // Large conference
        public void AudioUploadOptions_ShouldAcceptValidSpeakerRanges(int minSpeakers, int maxSpeakers)
        {
            // Arrange & Act
            var options = new AudioUploadOptions
            {
                DefaultMinSpeakers = minSpeakers,
                DefaultMaxSpeakers = maxSpeakers
            };

            // Assert
            Assert.Equal(minSpeakers, options.DefaultMinSpeakers);
            Assert.Equal(maxSpeakers, options.DefaultMaxSpeakers);
        }

        [Fact]
        public void AudioUploadOptions_BatchJobAutoRefreshSeconds_ShouldHaveDefaultValue()
        {
            // Arrange & Act
            var options = new AudioUploadOptions();

            // Assert
            Assert.Equal(60, options.BatchJobAutoRefreshSeconds);
        }

        [Fact]
        public void AudioUploadOptions_EnableBatchTranscription_ShouldDefaultToTrue()
        {
            // Arrange & Act
            var options = new AudioUploadOptions();

            // Assert
            Assert.True(options.EnableBatchTranscription);
        }

        [Fact]
        public void AudioUploadOptions_DefaultLocale_ShouldBeEnglishUS()
        {
            // Arrange & Act
            var options = new AudioUploadOptions();

            // Assert
            Assert.Equal("en-US", options.DefaultLocale);
        }
    }
}
