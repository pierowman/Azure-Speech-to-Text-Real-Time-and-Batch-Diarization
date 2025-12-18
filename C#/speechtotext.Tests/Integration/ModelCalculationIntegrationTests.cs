using FluentAssertions;
using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Integration;

public class ModelCalculationIntegrationTests
{
    [Fact]
    public void SpeakerSegment_AllCalculatedProperties_ShouldWorkTogether()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            Speaker = "Modified Speaker",
            OriginalSpeaker = "Original Speaker",
            Text = "Modified text",
            OriginalText = "Original text",
            OffsetInTicks = 50000000, // 5 seconds
            DurationInTicks = 30000000, // 3 seconds
            LineNumber = 5
        };

        // Act & Assert - All properties should calculate correctly
        segment.StartTimeInSeconds.Should().Be(5.0);
        segment.EndTimeInSeconds.Should().Be(8.0);
        segment.UIFormattedStartTime.Should().Be("00:00:05");
        segment.SpeakerWasChanged.Should().BeTrue();
        segment.TextWasChanged.Should().BeTrue();
        segment.LineNumber.Should().Be(5);
    }

    [Fact]
    public void TranscriptionResult_WithMultipleSegments_ShouldCalculateStatisticsCorrectly()
    {
        // Arrange
        var result = new TranscriptionResult
        {
            Success = true,
            Segments = new List<SpeakerSegment>
            {
                new() 
                { 
                    Speaker = "Guest-1", 
                    Text = "Hello",
                    OffsetInTicks = 0,
                    DurationInTicks = 10000000, // 1 second
                    LineNumber = 1
                },
                new() 
                { 
                    Speaker = "Guest-2", 
                    Text = "Hi there",
                    OffsetInTicks = 10000000,
                    DurationInTicks = 15000000, // 1.5 seconds
                    LineNumber = 2
                },
                new() 
                { 
                    Speaker = "Guest-1", 
                    Text = "How are you?",
                    OffsetInTicks = 25000000,
                    DurationInTicks = 20000000, // 2 seconds
                    LineNumber = 3
                }
            }
        };

        // Calculate statistics manually (simulating what service does)
        result.AvailableSpeakers = result.Segments
            .Select(s => s.Speaker)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        result.SpeakerStatistics = result.Segments
            .GroupBy(s => s.Speaker)
            .Select(g => new SpeakerInfo
            {
                Name = g.Key,
                SegmentCount = g.Count(),
                TotalSpeakTimeSeconds = g.Sum(s => s.EndTimeInSeconds - s.StartTimeInSeconds),
                FirstAppearanceSeconds = g.Min(s => s.StartTimeInSeconds)
            })
            .OrderBy(s => s.FirstAppearanceSeconds)
            .ToList();

        // Assert
        result.AvailableSpeakers.Should().HaveCount(2);
        result.AvailableSpeakers.Should().Contain(new[] { "Guest-1", "Guest-2" });

        result.SpeakerStatistics.Should().HaveCount(2);
        
        var guest1Stats = result.SpeakerStatistics.First(s => s.Name == "Guest-1");
        guest1Stats.SegmentCount.Should().Be(2);
        guest1Stats.TotalSpeakTimeSeconds.Should().Be(3.0); // 1 + 2 seconds
        guest1Stats.FirstAppearanceSeconds.Should().Be(0.0);

        var guest2Stats = result.SpeakerStatistics.First(s => s.Name == "Guest-2");
        guest2Stats.SegmentCount.Should().Be(1);
        guest2Stats.TotalSpeakTimeSeconds.Should().Be(1.5);
        guest2Stats.FirstAppearanceSeconds.Should().Be(1.0);
    }

    [Fact]
    public void SpeakerSegment_LineNumbering_ShouldBeSequential()
    {
        // Arrange
        var segments = new List<SpeakerSegment>();

        // Act - Simulate service assigning line numbers
        for (int i = 0; i < 10; i++)
        {
            var segment = new SpeakerSegment
            {
                Speaker = $"Guest-{i % 2 + 1}",
                Text = $"Segment {i}",
                LineNumber = i + 1
            };
            segments.Add(segment);
        }

        // Assert
        segments.Should().HaveCount(10);
        segments[0].LineNumber.Should().Be(1);
        segments[4].LineNumber.Should().Be(5);
        segments[9].LineNumber.Should().Be(10);
        
        // Line numbers should be sequential
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].LineNumber.Should().Be(i + 1);
        }
    }

    [Fact]
    public void SpeakerInfo_FormattedProperties_ShouldMatchRawValues()
    {
        // Arrange
        var speakerInfo = new SpeakerInfo
        {
            Name = "Guest-1",
            SegmentCount = 5,
            TotalSpeakTimeSeconds = 125.5, // 2 minutes, 5.5 seconds
            FirstAppearanceSeconds = 10.0
        };

        // Act & Assert
        speakerInfo.TotalSpeakTimeFormatted.Should().Be("00:02:05");
        speakerInfo.FirstAppearanceFormatted.Should().Be("00:00:10");
    }

    [Fact]
    public void TranscriptionResult_WithNoChanges_ShouldNotShowAsChanged()
    {
        // Arrange
        var segment = new SpeakerSegment
        {
            Speaker = "Guest-1",
            OriginalSpeaker = "Guest-1",
            Text = "Hello world",
            OriginalText = "Hello world"
        };

        // Act & Assert
        segment.SpeakerWasChanged.Should().BeFalse();
        segment.TextWasChanged.Should().BeFalse();
    }

    [Fact]
    public void TranscriptionResult_FilteredSegments_ShouldHaveSequentialLineNumbers()
    {
        // Arrange - Simulate filtering out empty segments
        var allSegments = new List<SpeakerSegment>
        {
            new() { Speaker = "Guest-1", Text = "Hello", OffsetInTicks = 0 },
            new() { Speaker = "Unknown", Text = "", OffsetInTicks = 10000000 }, // Should be filtered
            new() { Speaker = "Guest-2", Text = "Hi", OffsetInTicks = 20000000 },
            new() { Speaker = "Unknown", Text = "", OffsetInTicks = 30000000 }, // Should be filtered
            new() { Speaker = "Guest-1", Text = "Bye", OffsetInTicks = 40000000 }
        };

        // Act - Simulate service filtering and assigning line numbers
        var filteredSegments = allSegments
            .Where(s => !(s.Speaker.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(s.Text)))
            .ToList();

        for (int i = 0; i < filteredSegments.Count; i++)
        {
            filteredSegments[i].LineNumber = i + 1;
        }

        // Assert
        filteredSegments.Should().HaveCount(3);
        filteredSegments[0].LineNumber.Should().Be(1);
        filteredSegments[1].LineNumber.Should().Be(2);
        filteredSegments[2].LineNumber.Should().Be(3);
        filteredSegments.Select(s => s.Speaker).Should().NotContain("Unknown");
    }
}
