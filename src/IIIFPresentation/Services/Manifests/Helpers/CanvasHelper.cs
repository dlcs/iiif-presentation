using Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Services.Manifests.Settings;

namespace Services.Manifests.Helpers;

public class CanvasHelper(IOptions<ServicesSettings> options)
{
    private readonly ServicesSettings settings = options.Value;

    /// <summary>
    /// Checks for if the canvas id has any prohibited characters
    /// </summary>
    /// <param name="canvasId">The canvas id to check</param>
    /// <param name="logger">logger used to log when there's a prohibited character, if not set to throw an exception</param>
    /// <param name="throwException">Whether an exception should be thrown for invalid characters</param>
    /// <returns>the checked canvas id</returns>
    /// <remarks>This method will return null if not set to throw an exception and has invalid characters</remarks>
    public string? CheckForProhibitedCharacters(string canvasId, ILogger logger, bool throwException = true)
    {
        if (settings.ProhibitedCanvasIdCharacters.Any(canvasId.Contains))
        {
            if (throwException)
            {
                throw new InvalidCanvasIdException(canvasId,
                    $"Canvas id contains a prohibited character. Cannot contain any of: {settings.ProhibitedCanvasIdCharactersDisplay}");
            }

            logger.LogWarning(
                "Canvas id {CanvasId} contains a prohibited character. Cannot contain any of: {ProhibitedCharacterDisplay}",
                canvasId, settings.ProhibitedCanvasIdCharactersDisplay);

            return null;
        }

        return canvasId;
    }

    /// <summary>
    /// Checks a parsed canvas id for if parsed canvas id has any errors
    /// </summary>
    /// <param name="parsedCanvasId">The parsed canvas id to check</param>
    /// <param name="fullPath">The pre-parsed canvas id</param>
    /// <param name="logger">Logger for logging an error if throw exception is not set</param>
    /// <param name="throwException">Throws an exception if set for if there are invalid characters or the parsed path is null</param>
    /// <returns>the canvas id checked for errors</returns>
    public string? CheckParsedCanvasIdForErrors(PathParts parsedCanvasId, string fullPath, ILogger logger, bool throwException = true)
    {
        if (string.IsNullOrEmpty(parsedCanvasId.Resource))
        {
            if (throwException)
            {
                throw new InvalidCanvasIdException(fullPath);
            }
            return null;
        }

        return CheckForProhibitedCharacters(parsedCanvasId.Resource, logger, throwException);
    }
}
