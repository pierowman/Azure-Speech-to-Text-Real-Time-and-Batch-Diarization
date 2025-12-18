using FluentAssertions;
using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models;

public class FileTranscriptionInfoTests
{
    [Fact]
    public void Constructor_ShouldInitializeCollections()
    {
        // Act
        var fileInfo = new FileTranscriptionInfo();

        // Assert
        fileInfo.Segments.Should().NotBeNull().And.BeEmpty();
        fileInfo.AvailableSpeakers.Should().NotBeNull().And.BeEmpty();
        fileInfo.SpeakerStatistics.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo();

        // Act
        fileInfo.FileName = "meeting.wav";
        fileInfo.Channel = 0;
        fileInfo.FullTranscript = "Test transcript";
        fileInfo.DurationInTicks = 600000000; // 1 minute

        // Assert
        fileInfo.FileName.Should().Be("meeting.wav");
        fileInfo.Channel.Should().Be(0);
        fileInfo.FullTranscript.Should().Be("Test transcript");
        fileInfo.DurationInTicks.Should().Be(600000000);
    }

    [Fact]
    public void DurationInSeconds_ShouldCalculateCorrectly()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo
        {
            DurationInTicks = 600000000 // 60 seconds
        };

        // Act
        var durationInSeconds = fileInfo.DurationInSeconds;

        // Assert
        durationInSeconds.Should().BeApproximately(60.0, 0.1);
    }

    [Fact]
    public void DurationInSeconds_ShouldHandleZeroTicks()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo
        {
            DurationInTicks = 0
        };

        // Act
        var durationInSeconds = fileInfo.DurationInSeconds;

        // Assert
        durationInSeconds.Should().Be(0.0);
    }

    [Fact]
    public void DurationInSeconds_ShouldHandleLargeDurations()
    {
        // Arrange - 1 hour
        var fileInfo = new FileTranscriptionInfo
        {
            DurationInTicks = 36000000000 // 3600 seconds = 1 hour
        };

        // Act
        var durationInSeconds = fileInfo.DurationInSeconds;

        // Assert
        durationInSeconds.Should().BeApproximately(3600.0, 0.1);
    }

    [Fact]
    public void FormattedDuration_ShouldFormatCorrectly_ForShortDuration()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo
        {
            DurationInTicks = 300000000 // 30 seconds
        };

        // Act
        var formatted = fileInfo.FormattedDuration;

        // Assert
        formatted.Should().Be("00:00:30");
    }

    [Fact]
    public void FormattedDuration_ShouldFormatCorrectly_ForMinutes()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo
        {
            DurationInTicks = 9000000000 // 15 minutes
        };

        // Act
        var formatted = fileInfo.FormattedDuration;

        // Assert
        formatted.Should().Be("00:15:00");
    }

    [Fact]
    public void FormattedDuration_ShouldFormatCorrectly_ForHours()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo
        {
            DurationInTicks = 72000000000 // 2 hours
        };

        // Act
        var formatted = fileInfo.FormattedDuration;

        // Assert
        formatted.Should().Be("02:00:00");
    }

    [Fact]
    public void Segments_CanAddMultipleSegments()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo();

        // Act
        fileInfo.Segments.Add(new SpeakerSegment { Speaker = "Speaker 1", Text = "Hello" });
        fileInfo.Segments.Add(new SpeakerSegment { Speaker = "Speaker 2", Text = "Hi" });

        // Assert
        fileInfo.Segments.Should().HaveCount(2);
    }

    [Fact]
    public void AvailableSpeakers_CanContainMultipleSpeakers()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo();

        // Act
        fileInfo.AvailableSpeakers.Add("Speaker 1");
        fileInfo.AvailableSpeakers.Add("Speaker 2");

        // Assert
        fileInfo.AvailableSpeakers.Should().HaveCount(2);
        fileInfo.AvailableSpeakers.Should().Contain("Speaker 1");
    }

    [Fact]
    public void SpeakerStatistics_CanContainMultipleEntries()
    {
        // Arrange
        var fileInfo = new FileTranscriptionInfo();

        // Act
        fileInfo.SpeakerStatistics.Add(new SpeakerInfo 
        { 
            Name = "Speaker 1", 
            SegmentCount = 10,
            TotalSpeakTimeSeconds = 120
        });
        fileInfo.SpeakerStatistics.Add(new SpeakerInfo 
        { 
            Name = "Speaker 2", 
            SegmentCount = 8,
            TotalSpeakTimeSeconds = 90
        });

        // Assert
        fileInfo.SpeakerStatistics.Should().HaveCount(2);
        fileInfo.SpeakerStatistics[0].Name.Should().Be("Speaker 1");
        fileInfo.SpeakerStatistics[1].SegmentCount.Should().Be(8);
    }
}

public class BatchTranscriptionResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeFileResults()
    {
        // Act
        var result = new BatchTranscriptionResult();

        // Assert
        result.FileResults.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void TotalFiles_ShouldBeSettable()
    {
        // Arrange
        var result = new BatchTranscriptionResult();

        // Act
        result.TotalFiles = 3;

        // Assert
        result.TotalFiles.Should().Be(3);
    }

    [Fact]
    public void FileResults_CanAddMultipleFiles()
    {
        // Arrange
        var result = new BatchTranscriptionResult();

        // Act
        result.FileResults.Add(new FileTranscriptionInfo { FileName = "File 1" });
        result.FileResults.Add(new FileTranscriptionInfo { FileName = "File 2" });
        result.FileResults.Add(new FileTranscriptionInfo { FileName = "File 3" });

        // Assert
        result.FileResults.Should().HaveCount(3);
        result.FileResults[1].FileName.Should().Be("File 2");
    }

    [Fact]
    public void BatchTranscriptionResult_ShouldContainBothCombinedAndFileResults()
    {
        // Arrange
        var result = new BatchTranscriptionResult
        {
            Success = true,
            JobId = "job-123",
            DisplayName = "Multi-file Job",
            TotalFiles = 2
        };

        // Act - Add combined segments
        result.Segments.Add(new SpeakerSegment { Speaker = "Speaker 1", Text = "From file 1" });
        result.Segments.Add(new SpeakerSegment { Speaker = "Speaker 2", Text = "From file 2" });

        // Add per-file results
        result.FileResults.Add(new FileTranscriptionInfo 
        { 
            FileName = "File 1",
            Channel = 0,
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Speaker 1", Text = "From file 1" }
            }
        });
        result.FileResults.Add(new FileTranscriptionInfo 
        { 
            FileName = "File 2",
            Channel = 1,
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Speaker 2", Text = "From file 2" }
            }
        });

        // Assert
        result.Segments.Should().HaveCount(2); // Combined
        result.FileResults.Should().HaveCount(2); // Per-file
        result.TotalFiles.Should().Be(2);
        result.FileResults[0].Segments.Should().HaveCount(1);
        result.FileResults[1].Segments.Should().HaveCount(1);
    }

    [Fact]
    public void BatchTranscriptionResult_ShouldSupportSingleFileJob()
    {
        // Arrange
        var result = new BatchTranscriptionResult
        {
            Success = true,
            JobId = "job-456",
            DisplayName = "Single-file Job",
            TotalFiles = 1
        };

        // Act
        result.Segments.Add(new SpeakerSegment { Speaker = "Speaker 1", Text = "Only file" });
        result.FileResults.Add(new FileTranscriptionInfo 
        { 
            FileName = "File 1",
            Channel = 0,
            Segments = result.Segments
        });

        // Assert
        result.TotalFiles.Should().Be(1);
        result.FileResults.Should().HaveCount(1);
        result.FileResults[0].FileName.Should().Be("File 1");
    }
}
