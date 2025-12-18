using FluentAssertions;
using speechtotext.Exceptions;
using Xunit;

namespace speechtotext.Tests.Exceptions;

/// <summary>
/// Tests for InvalidAudioFileException to ensure proper exception behavior.
/// </summary>
public class InvalidAudioFileExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var expectedMessage = "Test error message";

        // Act
        var exception = new InvalidAudioFileException(expectedMessage);

        // Assert
        exception.Message.Should().Be(expectedMessage);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var expectedMessage = "Test error message";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new InvalidAudioFileException(expectedMessage, innerException);

        // Assert
        exception.Message.Should().Be(expectedMessage);
        exception.InnerException.Should().Be(innerException);
        exception.InnerException!.Message.Should().Be("Inner error");
    }

    [Fact]
    public void Exception_ShouldInheritFromException()
    {
        // Arrange & Act
        var exception = new InvalidAudioFileException("Test");

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Exception_ShouldBeThrowable()
    {
        // Arrange & Act
        Action action = () => throw new InvalidAudioFileException("Test error");

        // Assert
        action.Should().Throw<InvalidAudioFileException>()
            .WithMessage("Test error");
    }

    [Fact]
    public void Exception_ShouldBeCatchableAsBaseException()
    {
        // Arrange
        Exception? caughtException = null;

        // Act
        try
        {
            throw new InvalidAudioFileException("Test error");
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().NotBeNull();
        caughtException.Should().BeOfType<InvalidAudioFileException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Short")]
    [InlineData("A very long error message that contains multiple words and explains the validation error in detail")]
    public void Constructor_ShouldHandleVariousMessageLengths(string message)
    {
        // Act
        var exception = new InvalidAudioFileException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void Exception_WithInnerException_ShouldPreserveStackTrace()
    {
        // Arrange
        var innerException = new IOException("File access error");
        var exception = new InvalidAudioFileException("Validation failed", innerException);

        // Act & Assert
        exception.InnerException.Should().BeSameAs(innerException);
    }
}
