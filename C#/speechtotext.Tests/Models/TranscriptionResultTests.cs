using FluentAssertions;
using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models;

public class TranscriptionResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeCollections()
    {
        // Act
        var result = new TranscriptionResult();

        // Assert
        result.Segments.Should().NotBeNull().And.BeEmpty();
        result.AvailableSpeakers.Should().NotBeNull().And.BeEmpty();
        result.SpeakerStatistics.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var result = new TranscriptionResult();
        var segments = new List<SpeakerSegment>
        {
            new() { Speaker = "Guest-1", Text = "Hello" }
        };
        var speakers = new List<string> { "Guest-1", "Guest-2" };
        var stats = new List<SpeakerInfo>
        {
            new() { Name = "Guest-1", SegmentCount = 5 }
        };

        // Act
        result.Success = true;
        result.Message = "Test message";
        result.Segments = segments;
        result.FullTranscript = "Full transcript";
        result.AudioFileUrl = "/audio/test.wav";
        result.RawJsonData = "{}";
        result.GoldenRecordJsonData = "{}";
        result.AvailableSpeakers = speakers;
        result.SpeakerStatistics = stats;

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Test message");
        result.Segments.Should().BeEquivalentTo(segments);
        result.FullTranscript.Should().Be("Full transcript");
        result.AudioFileUrl.Should().Be("/audio/test.wav");
        result.RawJsonData.Should().Be("{}");
        result.GoldenRecordJsonData.Should().Be("{}");
        result.AvailableSpeakers.Should().BeEquivalentTo(speakers);
        result.SpeakerStatistics.Should().BeEquivalentTo(stats);
    }

    [Fact]
    public void Segments_CanAddMultipleItems()
    {
        // Arrange
        var result = new TranscriptionResult();

        // Act
        result.Segments.Add(new SpeakerSegment { Speaker = "Guest-1" });
        result.Segments.Add(new SpeakerSegment { Speaker = "Guest-2" });

        // Assert
        result.Segments.Should().HaveCount(2);
    }

    [Fact]
    public void AvailableSpeakers_CanContainMultipleSpeakers()
    {
        // Arrange
        var result = new TranscriptionResult();

        // Act
        result.AvailableSpeakers.Add("Guest-1");
        result.AvailableSpeakers.Add("Guest-2");
        result.AvailableSpeakers.Add("Guest-3");

        // Assert
        result.AvailableSpeakers.Should().HaveCount(3);
        result.AvailableSpeakers.Should().Contain("Guest-2");
    }
}
