using FluentAssertions;
using speechtotext.Exceptions;
using Xunit;

namespace speechtotext.Tests.Exceptions;

public class TranscriptionExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Test exception message";

        // Act
        var exception = new TranscriptionException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var message = "Test exception message";
        var innerException = new InvalidOperationException("Inner exception");

        // Act
        var exception = new TranscriptionException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void TranscriptionException_ShouldBeException()
    {
        // Arrange & Act
        var exception = new TranscriptionException("Test");

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }
}
