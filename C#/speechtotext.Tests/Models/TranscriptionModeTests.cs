using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models
{
    public class TranscriptionModeTests
    {
        [Fact]
        public void TranscriptionMode_ShouldHaveRealTimeValue()
        {
            // Arrange & Act
            var mode = TranscriptionMode.RealTime;

            // Assert
            Assert.Equal(0, (int)mode);
        }

        [Fact]
        public void TranscriptionMode_ShouldHaveBatchValue()
        {
            // Arrange & Act
            var mode = TranscriptionMode.Batch;

            // Assert
            Assert.Equal(1, (int)mode);
        }

        [Theory]
        [InlineData(TranscriptionMode.RealTime)]
        [InlineData(TranscriptionMode.Batch)]
        public void TranscriptionMode_ShouldBeValidEnumValue(TranscriptionMode mode)
        {
            // Assert
            Assert.True(Enum.IsDefined(typeof(TranscriptionMode), mode));
        }
    }
}
