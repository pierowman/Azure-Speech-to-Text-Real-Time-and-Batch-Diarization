using FluentAssertions;
using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models;

public class SpeakerInfoTests
{
    [Fact]
    public void TotalSpeakTimeFormatted_ShouldFormatSeconds_Correctly()
    {
        // Arrange
        var speakerInfo = new SpeakerInfo
        {
            TotalSpeakTimeSeconds = 3661 // 1 hour, 1 minute, 1 second
        };

        // Act
        var result = speakerInfo.TotalSpeakTimeFormatted;

        // Assert
        result.Should().Be("01:01:01");
    }

    [Fact]
    public void FirstAppearanceFormatted_ShouldFormatSeconds_Correctly()
    {
        // Arrange
        var speakerInfo = new SpeakerInfo
        {
            FirstAppearanceSeconds = 125.5 // 2 minutes, 5.5 seconds
        };

        // Act
        var result = speakerInfo.FirstAppearanceFormatted;

        // Assert
        result.Should().Be("00:02:05");
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(1, "00:00:01")]
    [InlineData(60, "00:01:00")]
    [InlineData(3600, "01:00:00")]
    [InlineData(3723, "01:02:03")]
    public void TotalSpeakTimeFormatted_ShouldHandleVariousTimeSpans(double seconds, string expected)
    {
        // Arrange
        var speakerInfo = new SpeakerInfo
        {
            TotalSpeakTimeSeconds = seconds
        };

        // Act
        var result = speakerInfo.TotalSpeakTimeFormatted;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var speakerInfo = new SpeakerInfo();

        // Act
        speakerInfo.Name = "Guest-1";
        speakerInfo.SegmentCount = 10;
        speakerInfo.TotalSpeakTimeSeconds = 123.45;
        speakerInfo.FirstAppearanceSeconds = 5.0;

        // Assert
        speakerInfo.Name.Should().Be("Guest-1");
        speakerInfo.SegmentCount.Should().Be(10);
        speakerInfo.TotalSpeakTimeSeconds.Should().Be(123.45);
        speakerInfo.FirstAppearanceSeconds.Should().Be(5.0);
    }
}
