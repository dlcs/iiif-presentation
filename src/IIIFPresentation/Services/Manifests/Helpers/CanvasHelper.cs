using Core.Exceptions;
using Microsoft.Extensions.Logging;
using Repository.Paths;

namespace Services.Manifests.Helpers;

public static class CanvasHelper
{
    private static readonly List<char> ProhibitedCharacters = ['/', '=', ',',];
    private static readonly string ProhibitedCharacterDisplay =
        string.Join(',', ProhibitedCharacters.Select(p => $"'{p}'"));
    
    public static string? CheckForProhibitedCharacters<T>(string canvasId, ILogger<T> logger, bool throwException = true)
    {
        if (ProhibitedCharacters.Any(canvasId.Contains))
        {
            if (throwException)
            {
                throw new InvalidCanvasIdException(canvasId,
                    $"Canvas id contains a prohibited character. Cannot contain any of: {ProhibitedCharacterDisplay}");
            }

            logger.LogWarning(
                "Canvas id {CanvasId} contains a prohibited character. Cannot contain any of: {ProhibitedCharacterDisplay}",
                canvasId, ProhibitedCharacterDisplay);
            
            return null;
        }
        
        return canvasId;
    }
    
    public static string? CheckParsedCanvasIdForErrors<T>(PathParts parsedCanvasId, string fullPath, ILogger<T> logger, bool throwException = true)
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
