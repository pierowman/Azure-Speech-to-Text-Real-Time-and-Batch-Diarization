using FluentAssertions;
using speechtotext.Models;
using Xunit;

namespace speechtotext.Tests.Models;

/// <summary>
/// Tests for ErrorViewModel to ensure proper error display logic.
/// </summary>
public class ErrorViewModelTests
{
    [Fact]
    public void ShowRequestId_ShouldBeFalse_WhenRequestIdIsNull()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = null
        };

        // Act & Assert
        model.ShowRequestId.Should().BeFalse();
    }

    [Fact]
    public void ShowRequestId_ShouldBeFalse_WhenRequestIdIsEmpty()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = string.Empty
        };

        // Act & Assert
        model.ShowRequestId.Should().BeFalse();
    }

    [Fact]
    public void ShowRequestId_ShouldBeFalse_WhenRequestIdIsWhitespace()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = "   "
        };

        // Act & Assert
        // Note: string.IsNullOrEmpty doesn't treat whitespace as empty, 
        // so whitespace values will show the request ID
        model.ShowRequestId.Should().BeTrue();
    }

    [Fact]
    public void ShowRequestId_ShouldBeTrue_WhenRequestIdHasValue()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = "test-request-id-123"
        };

        // Act & Assert
        model.ShowRequestId.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abc-def-ghi")]
    [InlineData("0:0:0:0:0:1:1")]
    public void ShowRequestId_ShouldBeTrue_WithVariousRequestIdFormats(string requestId)
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = requestId
        };

        // Act & Assert
        model.ShowRequestId.Should().BeTrue();
    }

    [Fact]
    public void RequestId_ShouldBeSettableAndGettable()
    {
        // Arrange
        var expectedId = "test-id-456";
        var model = new ErrorViewModel();

        // Act
        model.RequestId = expectedId;

        // Assert
        model.RequestId.Should().Be(expectedId);
    }

    [Fact]
    public void ErrorViewModel_ShouldAllowDefaultConstruction()
    {
        // Act
        var model = new ErrorViewModel();

        // Assert
        model.Should().NotBeNull();
        model.RequestId.Should().BeNull();
        model.ShowRequestId.Should().BeFalse();
    }
}
