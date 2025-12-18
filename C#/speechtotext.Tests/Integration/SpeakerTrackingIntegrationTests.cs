using FluentAssertions;
using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Integration;

/// <summary>
/// Integration tests for speaker tracking across multiple operations.
/// These tests verify that speaker lists and statistics remain consistent
/// through various editing scenarios.
/// </summary>
public class SpeakerTrackingIntegrationTests
{
    [Fact]
    public void SpeakerTracking_AfterInitialTranscription_ShouldHaveCorrectSpeakers()
    {
        // Arrange & Act - Simulate initial transcription result
        var result = new TranscriptionResult
        {
            Success = true,
            Segments = new List<SpeakerSegment>
            {
                new() 
                { 
                    Speaker = "Guest-1", 
                    OriginalSpeaker = "Guest-1",
                    Text = "Hello", 
                    OffsetInTicks = 0, 
                    DurationInTicks = 10000000,
                    LineNumber = 1
                },
                new() 
                { 
                    Speaker = "Guest-2", 
                    OriginalSpeaker = "Guest-2",
                    Text = "Hi", 
                    OffsetInTicks = 10000000, 
                    DurationInTicks = 10000000,
                    LineNumber = 2
                }
            }
        };

        // Calculate speakers and statistics (simulating service behavior)
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
        result.AvailableSpeakers.Should().ContainInOrder("Guest-1", "Guest-2");
        
        result.SpeakerStatistics.Should().HaveCount(2);
        result.SpeakerStatistics[0].Name.Should().Be("Guest-1");
        result.SpeakerStatistics[1].Name.Should().Be("Guest-2");
    }

    [Fact]
    public void SpeakerTracking_AfterEditingSpeakerName_ShouldUpdateAllReferences()
    {
        // Arrange - Initial state
        var segments = new List<SpeakerSegment>
        {
            new() 
            { 
                Speaker = "Guest-1", 
                OriginalSpeaker = "Guest-1",
                Text = "Hello", 
                OffsetInTicks = 0, 
                DurationInTicks = 10000000,
                LineNumber = 1
            },
            new() 
            { 
                Speaker = "Guest-2", 
                OriginalSpeaker = "Guest-2",
                Text = "Hi", 
                OffsetInTicks = 10000000, 
                DurationInTicks = 10000000,
                LineNumber = 2
            }
        };

        // Act - User edits Guest-1 to "Alice"
        segments[0].Speaker = "Alice";

        // Recalculate (simulating server response)
        var availableSpeakers = segments
            .Select(s => s.Speaker)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var speakerStatistics = segments
            .GroupBy(s => s.Speaker)
            .Select(g => new SpeakerInfo
            {
                Name = g.Key,
                SegmentCount = g.Count()
            })
            .OrderBy(s => s.FirstAppearanceSeconds)
            .ToList();

        // Assert
        availableSpeakers.Should().HaveCount(2);
        availableSpeakers.Should().Contain("Alice");
        availableSpeakers.Should().Contain("Guest-2");
        availableSpeakers.Should().NotContain("Guest-1");
        
        segments[0].SpeakerWasChanged.Should().BeTrue();
        segments[1].SpeakerWasChanged.Should().BeFalse();
    }

    [Fact]
    public void SpeakerTracking_AfterReassigningAllSegments_ShouldRemoveOldSpeaker()
    {
        // Arrange - Initial state with 2 speakers
        var segments = new List<SpeakerSegment>
        {
            new() { Speaker = "Guest-1", Text = "A", OffsetInTicks = 0, DurationInTicks = 10000000, LineNumber = 1 },
            new() { Speaker = "Guest-2", Text = "B", OffsetInTicks = 10000000, DurationInTicks = 10000000, LineNumber = 2 },
            new() { Speaker = "Guest-1", Text = "C", OffsetInTicks = 20000000, DurationInTicks = 10000000, LineNumber = 3 }
        };

        // Act - Reassign all Guest-1 segments to Guest-2
        foreach (var segment in segments.Where(s => s.Speaker == "Guest-1"))
        {
            segment.Speaker = "Guest-2";
        }

        // Recalculate
        var availableSpeakers = segments
            .Select(s => s.Speaker)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var speakerStatistics = segments
            .GroupBy(s => s.Speaker)
            .Select(g => new SpeakerInfo
            {
                Name = g.Key,
                SegmentCount = g.Count()
            })
            .ToList();

        // Assert
        availableSpeakers.Should().HaveCount(1);
        availableSpeakers.Should().Contain("Guest-2");
        availableSpeakers.Should().NotContain("Guest-1");
        
        speakerStatistics.Should().HaveCount(1);
        speakerStatistics[0].Name.Should().Be("Guest-2");
        speakerStatistics[0].SegmentCount.Should().Be(3);
    }

    [Fact]
    public void SpeakerTracking_AddingManualSpeaker_ShouldAppearInAvailableList()
    {
        // Arrange - Initial state
        var segments = new List<SpeakerSegment>
        {
            new() { Speaker = "Guest-1", Text = "Hello", OffsetInTicks = 0, DurationInTicks = 10000000, LineNumber = 1 }
        };

        var availableSpeakers = segments
            .Select(s => s.Speaker)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        // Act - User manually adds a new speaker (not yet assigned to any segment)
        var manuallyAddedSpeaker = "Alice";
        if (!availableSpeakers.Contains(manuallyAddedSpeaker))
        {
            availableSpeakers.Add(manuallyAddedSpeaker);
            availableSpeakers = availableSpeakers.OrderBy(s => s).ToList();
        }

        // Assert
        availableSpeakers.Should().HaveCount(2);
        availableSpeakers.Should().Contain("Alice");
        availableSpeakers.Should().Contain("Guest-1");
        availableSpeakers.Should().BeInAscendingOrder();
    }

    [Fact]
    public void SpeakerTracking_AfterComplexEditingSequence_ShouldMaintainConsistency()
    {
        // Arrange - Initial state with 3 speakers
        var segments = new List<SpeakerSegment>
        {
            new() { Speaker = "Guest-1", OriginalSpeaker = "Guest-1", Text = "A", OffsetInTicks = 0, DurationInTicks = 10000000, LineNumber = 1 },
            new() { Speaker = "Guest-2", OriginalSpeaker = "Guest-2", Text = "B", OffsetInTicks = 10000000, DurationInTicks = 10000000, LineNumber = 2 },
            new() { Speaker = "Guest-3", OriginalSpeaker = "Guest-3", Text = "C", OffsetInTicks = 20000000, DurationInTicks = 10000000, LineNumber = 3 },
            new() { Speaker = "Guest-1", OriginalSpeaker = "Guest-1", Text = "D", OffsetInTicks = 30000000, DurationInTicks = 10000000, LineNumber = 4 }
        };

        // Act - Step 1: Edit Guest-1 at line 1 to "Alice"
        segments[0].Speaker = "Alice";

        // Step 2: Reassign remaining Guest-1 to Guest-2
        segments[3].Speaker = "Guest-2";

        // Step 3: Edit Guest-3 to "Bob"
        segments[2].Speaker = "Bob";

        // Recalculate
        var availableSpeakers = segments
            .Select(s => s.Speaker)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var speakerStatistics = segments
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
        availableSpeakers.Should().HaveCount(3);
        availableSpeakers.Should().ContainInOrder("Alice", "Bob", "Guest-2");
        availableSpeakers.Should().NotContain("Guest-1");
        availableSpeakers.Should().NotContain("Guest-3");
        
        speakerStatistics.Should().HaveCount(3);
        speakerStatistics[0].Name.Should().Be("Alice");
        speakerStatistics[0].SegmentCount.Should().Be(1);
        
        speakerStatistics[1].Name.Should().Be("Guest-2");
        speakerStatistics[1].SegmentCount.Should().Be(2);
        
        speakerStatistics[2].Name.Should().Be("Bob");
        speakerStatistics[2].SegmentCount.Should().Be(1);
    }

    [Fact]
    public void SpeakerTracking_AfterTextEdit_ShouldNotAffectSpeakerLists()
    {
        // Arrange - Initial state
        var segments = new List<SpeakerSegment>
        {
            new() 
            { 
                Speaker = "Guest-1", 
                Text = "Original text", 
                OriginalText = "Original text",
                OffsetInTicks = 0, 
                DurationInTicks = 10000000,
                LineNumber = 1
            }
        };

        var initialAvailableSpeakers = segments
            .Select(s => s.Speaker)
            .Distinct()
            .ToList();

        // Act - Edit text only, not speaker
        segments[0].Text = "Modified text";

        var finalAvailableSpeakers = segments
            .Select(s => s.Speaker)
            .Distinct()
            .ToList();

        // Assert
        finalAvailableSpeakers.Should().BeEquivalentTo(initialAvailableSpeakers);
        segments[0].TextWasChanged.Should().BeTrue();
        segments[0].SpeakerWasChanged.Should().BeFalse();
    }

    [Fact]
    public void SpeakerTracking_WithCaseSensitiveSpeakers_ShouldTreatAsDifferent()
    {
        // Arrange & Act
        var segments = new List<SpeakerSegment>
        {
            new() { Speaker = "Alice", Text = "A", OffsetInTicks = 0, DurationInTicks = 10000000, LineNumber = 1 },
            new() { Speaker = "alice", Text = "B", OffsetInTicks = 10000000, DurationInTicks = 10000000, LineNumber = 2 },
            new() { Speaker = "ALICE", Text = "C", OffsetInTicks = 20000000, DurationInTicks = 10000000, LineNumber = 3 }
        };

        var availableSpeakers = segments
            .Select(s => s.Speaker)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var speakerStatistics = segments
            .GroupBy(s => s.Speaker)
            .Select(g => new SpeakerInfo
            {
                Name = g.Key,
                SegmentCount = g.Count()
            })
            .ToList();

        // Assert - Case-sensitive comparison should treat these as different speakers
        availableSpeakers.Should().HaveCount(3);
        availableSpeakers.Should().Contain(new[] { "ALICE", "Alice", "alice" });
        
        speakerStatistics.Should().HaveCount(3);
        speakerStatistics.Should().OnlyContain(s => s.SegmentCount == 1);
    }

    [Fact]
    public void SpeakerTracking_DeleteUnusedSpeaker_ShouldNotAffectSegments()
    {
        // Arrange
        var segments = new List<SpeakerSegment>
        {
            new() { Speaker = "Guest-1", Text = "Hello", OffsetInTicks = 0, DurationInTicks = 10000000, LineNumber = 1 }
        };

        var availableSpeakers = new List<string> { "Guest-1", "UnusedSpeaker" };

        // Act - Remove unused speaker from available list
        var speakersInUse = segments.Select(s => s.Speaker).Distinct().ToList();
        availableSpeakers = availableSpeakers.Where(s => speakersInUse.Contains(s)).ToList();

        // Assert
        availableSpeakers.Should().HaveCount(1);
        availableSpeakers.Should().Contain("Guest-1");
        availableSpeakers.Should().NotContain("UnusedSpeaker");
        segments.Should().HaveCount(1); // Segments unchanged
    }

    [Fact]
    public void SpeakerTracking_LineNumbering_ShouldRemainConsistentAfterEdits()
    {
        // Arrange
        var segments = new List<SpeakerSegment>
        {
            new() { Speaker = "Guest-1", Text = "A", OffsetInTicks = 0, DurationInTicks = 10000000, LineNumber = 1 },
            new() { Speaker = "Guest-2", Text = "B", OffsetInTicks = 10000000, DurationInTicks = 10000000, LineNumber = 2 },
            new() { Speaker = "Guest-1", Text = "C", OffsetInTicks = 20000000, DurationInTicks = 10000000, LineNumber = 3 }
        };

        // Act - Edit speakers but line numbers should remain
        segments[0].Speaker = "Alice";
        segments[2].Speaker = "Bob";

        // Assert
        segments[0].LineNumber.Should().Be(1);
        segments[1].LineNumber.Should().Be(2);
        segments[2].LineNumber.Should().Be(3);
        
        // Line numbers should be sequential regardless of speaker changes
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].LineNumber.Should().Be(i + 1);
        }
    }
}
