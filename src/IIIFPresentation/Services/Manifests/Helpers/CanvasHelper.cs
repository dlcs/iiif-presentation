using Core.Exceptions;
using Repository.Paths;

namespace Services.Manifests.Helpers;

public static class CanvasHelper
{
    private static readonly List<char> ProhibitedCharacters = ['/', '=', ',',];
    private static readonly string ProhibitedCharacterDisplay =
        string.Join(',', ProhibitedCharacters.Select(p => $"'{p}'"));
    
    public static void CheckForProhibitedCharacters(string canvasId)
    {
        if (ProhibitedCharacters.Any(canvasId.Contains))
        {
            throw new InvalidCanvasIdException(canvasId,
                $"Canvas Id {canvasId} contains a prohibited character. Cannot contain any of: {ProhibitedCharacterDisplay}");
        }
    }
    
    public static void CheckParsedCanvasIdForErrors(PathParts parsedCanvasId, string fullPath)
    {
        if (string.IsNullOrEmpty(parsedCanvasId.Resource))
        {
            throw new InvalidCanvasIdException(fullPath);
        }

        CheckForProhibitedCharacters(parsedCanvasId.Resource);
    }
}
