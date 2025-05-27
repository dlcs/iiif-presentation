using Core.Exceptions;
using System.Text.RegularExpressions;
using Core.Helpers;
using Core.Web;
using IIIF.Presentation.V3;
using Models.API.General;
using Models.API.Manifest;
using Models.DLCS;

namespace Repository.Paths;

/// <summary>
/// Helper class for parsing paths to extract elements (identifiers most likely)
/// </summary>
public static class PathParser
{
    private const char PathSeparator = '/';
    
    public static AssetId GetAssetIdFromNamedQueryCanvasId(this Canvas canvas, ILogger? logger = null)
    {
        var canvasId = canvas.Id.ThrowIfNullOrWhiteSpace(nameof(canvas.Id));

        try
        {
            var assetParts =
                canvasId[..canvasId.IndexOf("/canvas/c/", StringComparison.OrdinalIgnoreCase)].Split(PathSeparator)[^3..];
            return AssetId.FromString(string.Join(PathSeparator, assetParts));
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Error while parsing NQ canvas Id {CanvasId}", canvas.Id);
            throw new FormatException($"Unable to extract AssetId from {canvas.Id}");
        }
    }

    public static string GetCanvasId(CanvasPainting canvasPainting, int customerId)
    {
        var canvasId = canvasPainting.CanvasId.ThrowIfNull(nameof(canvasPainting));

        if (!Uri.IsWellFormedUriString(canvasId, UriKind.Absolute))
        {
            CheckForProhibitedCharacters(canvasId);
            return canvasId;
        }

        var convertedCanvasId = new Uri(canvasId).PathAndQuery;
        var customerCanvasesPath = $"/{customerId}/canvases/";

        if (!convertedCanvasId.StartsWith(customerCanvasesPath) || convertedCanvasId.Equals(customerCanvasesPath))
        {
            throw new InvalidCanvasIdException(convertedCanvasId);
        }

        var actualCanvasId = convertedCanvasId[customerCanvasesPath.Length..];
        CheckForProhibitedCharacters(actualCanvasId);

        return actualCanvasId;
    }

    private static void CheckForProhibitedCharacters(string canvasId)
    {
        if (ProhibitedCharacters.Any(canvasId.Contains))
        {
            throw new InvalidCanvasIdException(canvasId,
                $"Canvas Id {canvasId} contains a prohibited character. Cannot contain any of: {ProhibitedCharacterDisplay}");
        }
    }

    /// <summary>
    /// Gets a hierarchical path from a full array of path elements
    /// </summary>
    public static string GetHierarchicalPath(string[] pathElements) =>
        string.Join(PathSeparator, pathElements.Skip(2).SkipLast(1)); // skip customer id and trailing whitespace 

    /// <summary>
    /// Gets the resource id from a full array of path elements
    /// </summary>
    public static string GetResourceIdFromPath(string[] pathElements) =>
        pathElements.SkipLast(1).Last(); // miss the trailing whitespace and use the last path element

    /// <summary>
    /// Retrieves the slug from a fully qualified hierarchical path
    /// </summary>
    /// <exception cref="UriFormatException">When the path isn't a URI</exception>
    public static string GetSlugFromHierarchicalPath(string path, int customerId)
    {
        var lastPath = path.GetLastPathElement();
        var host = new Uri(path).Host;
        
        // this is the root collection
        if (host == lastPath || lastPath.Equals(customerId.ToString()))
        {
            return string.Empty;
        }
        
        return lastPath;
    }

    /// <summary>
    /// This is the index of a customer id from a full path
    /// </summary>
    public static int FullPathCustomerIdIndex => 1;
    
    /// <summary>
    /// Index of the element used for the type of path
    /// </summary>
    public static int FullPathTypeIndex => 2;

    public static Uri GetParentUriFromPublicId(string publicId) => 
        new(publicId[..publicId.LastIndexOf(PathSeparator)]);

    private static readonly List<char> ProhibitedCharacters = ['/', '=', '=', ',',];
    private static string ProhibitedCharacterDisplay = string.Join(',', ProhibitedCharacters.Select(p => $"'{p}'"));

}
