using Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;

namespace Services.Tests.Manifests.Helpers;

public class CanvasHelperTests
{
    ILogger<CanvasHelperTests> logger = new NullLogger<CanvasHelperTests>();
    private readonly CanvasHelper sut = new(Options.Create(new ServicesSettings()));

    [Fact]
    public void CheckForProhibitedCharacters_AllowsString_WithNoProhibitedCharacters()
    {
        // Arrange
        var stringToCheck = "valid";

        // Act
        Action action = () => sut.CheckForProhibitedCharacters(stringToCheck, logger);

        // Assert
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData('/')]
    [InlineData('=')]
    [InlineData(',')]
    public void CheckForProhibitedCharacters_ThrowsError_WithProhibitedCharacters(char invalidCharacter)
    {
        // Arrange
        var stringToCheck = "invalid" + invalidCharacter;

        // Act
        Action action = () => sut.CheckForProhibitedCharacters(stringToCheck, logger);

        // Assert
        action.Should().ThrowExactly<InvalidCanvasIdException>();
    }

    [Theory]
    [InlineData('/')]
    [InlineData('=')]
    [InlineData(',')]
    public void CheckForProhibitedCharacters_DoesNotThrowError_WithProhibitedCharactersWithThrowErrorsFalse(char invalidCharacter)
    {
        // Arrange
        var stringToCheck = "invalid" + invalidCharacter;

        // Act
        var canvasId = sut.CheckForProhibitedCharacters(stringToCheck, logger, false);

        // Assert
        canvasId.Should().BeNull();
    }

    [Fact]
    public void CheckParsedCanvasIdForErrors_AllowsCanvasId_WithNoProhibitedCharacters()
    {
        // Arrange
        var pathPartsToCheck = new PathParts(1, "valid", false);

        // Act
        Action action = () => sut.CheckParsedCanvasIdForErrors(pathPartsToCheck, "/some/path", logger);

        // Assert
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData('/')]
    [InlineData('=')]
    [InlineData(',')]
    public void CheckParsedCanvasIdForErrors_ThrowsError_WithProhibitedCharacters(char invalidCharacter)
    {
        // Arrange
        var pathPartsToCheck = new PathParts(1, "invalid" + invalidCharacter, false);

        // Act
        Action action = () => sut.CheckParsedCanvasIdForErrors(pathPartsToCheck, "some/path", logger);

        // Assert
        action.Should().ThrowExactly<InvalidCanvasIdException>();
    }

    [Theory]
    [InlineData('/')]
    [InlineData('=')]
    [InlineData(',')]
    public void CheckParsedCanvasIdForErrors_DoesNotThrowError_WithProhibitedCharactersWhenthrowErrorsFalse(char invalidCharacter)
    {
        // Arrange
        var pathPartsToCheck = new PathParts(1, "invalid" + invalidCharacter, false);

        // Act
        var canvasId =  sut.CheckParsedCanvasIdForErrors(pathPartsToCheck, "some/path", logger, false);

        // Assert
        canvasId.Should().BeNull();
    }

    [Fact]
    public void CheckParsedCanvasIdForErrors_ThrowsError_WhenPathPartsNull()
    {
        // Arrange
        var pathPartsToCheck = new PathParts(1, null, false);

        // Act
        Action action = () => sut.CheckParsedCanvasIdForErrors(pathPartsToCheck, "some/path", logger);

        // Assert
        action.Should().ThrowExactly<InvalidCanvasIdException>();
    }

    [Fact]
    public void CheckParsedCanvasIdForErrors_DoesNotThrowError_WhenPathPartsNullWithThrowErrorsFalse()
    {
        // Arrange
        var pathPartsToCheck = new PathParts(1, null, false);

        // Act
        var canvasId = sut.CheckParsedCanvasIdForErrors(pathPartsToCheck, "some/path", logger, false);

        // Assert
        canvasId.Should().BeNull();
    }
}
