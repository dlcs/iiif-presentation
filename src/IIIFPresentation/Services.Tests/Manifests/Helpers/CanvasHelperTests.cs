using Core.Exceptions;
using Repository.Paths;
using Services.Manifests.Helpers;

namespace Services.Tests.Manifests.Helpers;

public class CanvasHelperTests
{
    [Fact]
    public void CheckForProhibitedCharacters_AllowsString_WithNoProhibitedCharacters()
    {
        // Arrange
        var stringToCheck = "valid";

        // Act
        Action action = () => CanvasHelper.CheckForProhibitedCharacters(stringToCheck);

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
        Action action = () => CanvasHelper.CheckForProhibitedCharacters(stringToCheck);

        // Assert
        action.Should().ThrowExactly<InvalidCanvasIdException>();
    }
    
    [Fact]
    public void CheckParsedCanvasIdForErrors_AllowsCanvasId_WithNoProhibitedCharacters()
    {
        // Arrange
        var pathPartsToCheck = new PathParts(1, "valid", false);

        // Act
        Action action = () => CanvasHelper.CheckParsedCanvasIdForErrors(pathPartsToCheck, "/some/path");

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
        Action action = () => CanvasHelper.CheckParsedCanvasIdForErrors(pathPartsToCheck, "some/path");

        // Assert
        action.Should().ThrowExactly<InvalidCanvasIdException>();
    }
    
    [Fact]
    public void CheckParsedCanvasIdForErrors_ThrowsError_WhenPathPartsNull()
    {
        // Arrange
        var pathPartsToCheck = new PathParts(1, null, false);

        // Act
        Action action = () => CanvasHelper.CheckParsedCanvasIdForErrors(pathPartsToCheck, "some/path");

        // Assert
        action.Should().ThrowExactly<InvalidCanvasIdException>();
    }
}
