using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using speechtotext.Controllers;
using speechtotext.Models;
using speechtotext.Services;
using speechtotext.Validation;
using Xunit;

namespace speechtotext.Tests.Controllers;

/// <summary>
/// Tests for speaker tracking functionality in HomeController.
/// These tests verify that AvailableSpeakers and SpeakerStatistics are correctly
/// calculated and returned after various operations.
/// </summary>
public class HomeControllerSpeakerTrackingTests
{
    private readonly Mock<ILogger<HomeController>> _mockLogger;
    private readonly Mock<ISpeechToTextService> _mockSpeechService;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<IAudioFileValidator> _mockValidator;
    private readonly Mock<IOptions<AudioUploadOptions>> _mockOptions;
    private readonly Mock<ITranscriptionJobService> _mockJobService;
    private readonly Mock<IBatchTranscriptionService> _mockBatchService;
    private readonly HomeController _controller;

    public HomeControllerSpeakerTrackingTests()
    {
        _mockLogger = new Mock<ILogger<HomeController>>();
        _mockSpeechService = new Mock<ISpeechToTextService>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockValidator = new Mock<IAudioFileValidator>();
        _mockOptions = new Mock<IOptions<AudioUploadOptions>>();
        _mockJobService = new Mock<ITranscriptionJobService>();
        _mockBatchService = new Mock<IBatchTranscriptionService>();

        var options = new AudioUploadOptions
        {
            RealTimeAllowedExtensions = new[] { ".wav" },
            RealTimeMaxFileSizeInBytes = 26214400,
            BatchAllowedExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac" },
            BatchMaxFileSizeInBytes = 104857600,
            UploadFolderPath = "uploads"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockEnvironment.Setup(x => x.WebRootPath).Returns("C:\\test\\wwwroot");

        _controller = new HomeController(
            _mockLogger.Object,
            _mockSpeechService.Object,
            _mockEnvironment.Object,
            _mockValidator.Object,
            _mockOptions.Object,
            _mockJobService.Object,
            _mockBatchService.Object);
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldReturnAvailableSpeakers_WhenSingleSpeaker()
    {
        // Arrange
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Guest-1", Text = "Hello", OffsetInTicks = 0, DurationInTicks = 10000000 }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        transcriptionResult.AvailableSpeakers.Should().NotBeNull();
        transcriptionResult.AvailableSpeakers.Should().HaveCount(1);
        transcriptionResult.AvailableSpeakers.Should().Contain("Guest-1");
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldReturnAvailableSpeakers_WhenMultipleSpeakers()
    {
        // Arrange
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Guest-1", Text = "Hello", OffsetInTicks = 0, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-2", Text = "Hi there", OffsetInTicks = 10000000, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-1", Text = "How are you?", OffsetInTicks = 20000000, DurationInTicks = 10000000 }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        transcriptionResult.AvailableSpeakers.Should().NotBeNull();
        transcriptionResult.AvailableSpeakers.Should().HaveCount(2);
        transcriptionResult.AvailableSpeakers.Should().Contain(new[] { "Guest-1", "Guest-2" });
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldReturnSortedAvailableSpeakers()
    {
        // Arrange
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Charlie", Text = "Hello", OffsetInTicks = 0, DurationInTicks = 10000000 },
                new() { Speaker = "Alice", Text = "Hi", OffsetInTicks = 10000000, DurationInTicks = 10000000 },
                new() { Speaker = "Bob", Text = "Hey", OffsetInTicks = 20000000, DurationInTicks = 10000000 }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        transcriptionResult.AvailableSpeakers.Should().NotBeNull();
        transcriptionResult.AvailableSpeakers.Should().HaveCount(3);
        transcriptionResult.AvailableSpeakers.Should().BeInAscendingOrder();
        transcriptionResult.AvailableSpeakers.Should().ContainInOrder("Alice", "Bob", "Charlie");
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldNotIncludeDuplicateSpeakers()
    {
        // Arrange
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Guest-1", Text = "First", OffsetInTicks = 0, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-1", Text = "Second", OffsetInTicks = 10000000, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-1", Text = "Third", OffsetInTicks = 20000000, DurationInTicks = 10000000 }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        transcriptionResult.AvailableSpeakers.Should().NotBeNull();
        transcriptionResult.AvailableSpeakers.Should().HaveCount(1);
        transcriptionResult.AvailableSpeakers.Should().Contain("Guest-1");
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldReturnSpeakerStatistics_WithCorrectCounts()
    {
        // Arrange
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Guest-1", Text = "A", OffsetInTicks = 0, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-2", Text = "B", OffsetInTicks = 10000000, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-1", Text = "C", OffsetInTicks = 20000000, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-1", Text = "D", OffsetInTicks = 30000000, DurationInTicks = 10000000 }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        transcriptionResult.SpeakerStatistics.Should().NotBeNull();
        transcriptionResult.SpeakerStatistics.Should().HaveCount(2);
        
        var guest1Stats = transcriptionResult.SpeakerStatistics.First(s => s.Name == "Guest-1");
        guest1Stats.SegmentCount.Should().Be(3);
        
        var guest2Stats = transcriptionResult.SpeakerStatistics.First(s => s.Name == "Guest-2");
        guest2Stats.SegmentCount.Should().Be(1);
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldReturnSpeakerStatistics_WithCorrectTotalSpeakTime()
    {
        // Arrange
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Guest-1", Text = "A", OffsetInTicks = 0, DurationInTicks = 10000000 }, // 1 sec
                new() { Speaker = "Guest-2", Text = "B", OffsetInTicks = 10000000, DurationInTicks = 20000000 }, // 2 sec
                new() { Speaker = "Guest-1", Text = "C", OffsetInTicks = 30000000, DurationInTicks = 30000000 } // 3 sec
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        var guest1Stats = transcriptionResult.SpeakerStatistics.First(s => s.Name == "Guest-1");
        guest1Stats.TotalSpeakTimeSeconds.Should().Be(4.0); // 1 + 3 = 4 seconds
        
        var guest2Stats = transcriptionResult.SpeakerStatistics.First(s => s.Name == "Guest-2");
        guest2Stats.TotalSpeakTimeSeconds.Should().Be(2.0); // 2 seconds
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldReturnSpeakerStatistics_OrderedByFirstAppearance()
    {
        // Arrange
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Guest-2", Text = "A", OffsetInTicks = 0, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-1", Text = "B", OffsetInTicks = 10000000, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-3", Text = "C", OffsetInTicks = 20000000, DurationInTicks = 10000000 }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        transcriptionResult.SpeakerStatistics.Should().NotBeNull();
        transcriptionResult.SpeakerStatistics.Should().HaveCount(3);
        
        // Should be ordered by first appearance (Guest-2, Guest-1, Guest-3)
        transcriptionResult.SpeakerStatistics[0].Name.Should().Be("Guest-2");
        transcriptionResult.SpeakerStatistics[0].FirstAppearanceSeconds.Should().Be(0.0);
        
        transcriptionResult.SpeakerStatistics[1].Name.Should().Be("Guest-1");
        transcriptionResult.SpeakerStatistics[1].FirstAppearanceSeconds.Should().Be(1.0);
        
        transcriptionResult.SpeakerStatistics[2].Name.Should().Be("Guest-3");
        transcriptionResult.SpeakerStatistics[2].FirstAppearanceSeconds.Should().Be(2.0);
    }

    [Fact]
    public void UpdateSpeakerNames_AfterSpeakerReassignment_ShouldUpdateAvailableSpeakers()
    {
        // Arrange - Simulate reassigning all segments from Guest-1 to Guest-2
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Guest-2", Text = "A", OffsetInTicks = 0, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-2", Text = "B", OffsetInTicks = 10000000, DurationInTicks = 10000000 },
                new() { Speaker = "Guest-2", Text = "C", OffsetInTicks = 20000000, DurationInTicks = 10000000 }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        // After reassignment, only Guest-2 should exist
        transcriptionResult.AvailableSpeakers.Should().HaveCount(1);
        transcriptionResult.AvailableSpeakers.Should().Contain("Guest-2");
        transcriptionResult.AvailableSpeakers.Should().NotContain("Guest-1");
        
        transcriptionResult.SpeakerStatistics.Should().HaveCount(1);
        transcriptionResult.SpeakerStatistics[0].Name.Should().Be("Guest-2");
        transcriptionResult.SpeakerStatistics[0].SegmentCount.Should().Be(3);
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldIgnoreWhitespaceSpeakers()
    {
        // Arrange
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Guest-1", Text = "Hello", OffsetInTicks = 0, DurationInTicks = 10000000 }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        // Whitespace-only speakers should not be included
        transcriptionResult.AvailableSpeakers.Should().NotContain(string.Empty);
        transcriptionResult.AvailableSpeakers.Should().NotContain(" ");
    }

    [Fact]
    public void UpdateSpeakerNames_WithChangedSpeakerNames_ShouldReturnUpdatedStatistics()
    {
        // Arrange - Simulate editing speaker name from Guest-1 to Alice
        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() 
                { 
                    Speaker = "Alice", 
                    OriginalSpeaker = "Guest-1",
                    Text = "Hello", 
                    OffsetInTicks = 0, 
                    DurationInTicks = 10000000 
                },
                new() 
                { 
                    Speaker = "Guest-2", 
                    Text = "Hi", 
                    OffsetInTicks = 10000000, 
                    DurationInTicks = 10000000 
                }
            }
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        transcriptionResult.AvailableSpeakers.Should().HaveCount(2);
        transcriptionResult.AvailableSpeakers.Should().Contain("Alice");
        transcriptionResult.AvailableSpeakers.Should().Contain("Guest-2");
        transcriptionResult.AvailableSpeakers.Should().NotContain("Guest-1");
        
        var aliceStats = transcriptionResult.SpeakerStatistics.First(s => s.Name == "Alice");
        aliceStats.SegmentCount.Should().Be(1);
    }

    [Fact]
    public void UpdateSpeakerNames_ShouldPassThroughAuditLog()
    {
        // Arrange
        var auditLog = new List<AuditLogEntry>
        {
            new() 
            { 
                ChangeType = "SpeakerEdit", 
                LineNumber = 1, 
                OldValue = "Guest-1", 
                NewValue = "Alice" 
            }
        };

        var request = new UpdateSpeakersRequest
        {
            Segments = new List<SpeakerSegment>
            {
                new() { Speaker = "Alice", Text = "Hello", OffsetInTicks = 0, DurationInTicks = 10000000 }
            },
            AuditLog = auditLog
        };

        // Act
        var result = _controller.UpdateSpeakerNames(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var transcriptionResult = jsonResult.Value.Should().BeOfType<TranscriptionResult>().Subject;
        
        transcriptionResult.AuditLog.Should().NotBeNull();
        transcriptionResult.AuditLog.Should().HaveCount(1);
        transcriptionResult.AuditLog[0].ChangeType.Should().Be("SpeakerEdit");
        transcriptionResult.AuditLog[0].OldValue.Should().Be("Guest-1");
        transcriptionResult.AuditLog[0].NewValue.Should().Be("Alice");
    }
}
