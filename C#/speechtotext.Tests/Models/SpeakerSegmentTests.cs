using FluentAssertions;
using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models;

public class SpeakerSegmentTests
{
    [Fact]
    public void StartTimeInSeconds_ShouldConvertFromTicks_Correctly()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            OffsetInTicks = 10000000 // 1 second in 100-nanosecond units
        };

        // Act
        var result = segment.StartTimeInSeconds;

        // Assert
        result.Should().Be(1.0);
    }

    [Fact]
    public void EndTimeInSeconds_ShouldCalculate_Correctly()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            OffsetInTicks = 10000000, // 1 second
            DurationInTicks = 20000000 // 2 seconds
        };

        // Act
        var result = segment.EndTimeInSeconds;

        // Assert
        result.Should().Be(3.0); // 1 + 2 = 3 seconds
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(10000000, "00:00:01")]
    [InlineData(600000000, "00:01:00")]
    [InlineData(36000000000, "01:00:00")]
    public void UIFormattedStartTime_ShouldFormatCorrectly(long ticks, string expected)
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            OffsetInTicks = ticks
        };

        // Act
        var result = segment.UIFormattedStartTime;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SpeakerWasChanged_ShouldReturnTrue_WhenSpeakerChanged()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            Speaker = "Speaker-1",
            OriginalSpeaker = "Guest-1"
        };

        // Act
        var result = segment.SpeakerWasChanged;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SpeakerWasChanged_ShouldReturnFalse_WhenSpeakerNotChanged()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            Speaker = "Guest-1",
            OriginalSpeaker = "Guest-1"
        };

        // Act
        var result = segment.SpeakerWasChanged;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SpeakerWasChanged_ShouldReturnFalse_WhenOriginalSpeakerIsNull()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            Speaker = "Guest-1",
            OriginalSpeaker = null
        };

        // Act
        var result = segment.SpeakerWasChanged;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TextWasChanged_ShouldReturnTrue_WhenTextChanged()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            Text = "Modified text",
            OriginalText = "Original text"
        };

        // Act
        var result = segment.TextWasChanged;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TextWasChanged_ShouldReturnFalse_WhenTextNotChanged()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            Text = "Same text",
            OriginalText = "Same text"
        };

        // Act
        var result = segment.TextWasChanged;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TextWasChanged_ShouldReturnFalse_WhenOriginalTextIsNull()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            Text = "Some text",
            OriginalText = null
        };

        // Act
        var result = segment.TextWasChanged;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void LineNumber_ShouldBeSettable()
    {
        // Arrange
        var segment = new SpeakerSegment();

        // Act
        segment.LineNumber = 42;

        // Assert
        segment.LineNumber.Should().Be(42);
    }
}
