using Core.Helpers;
using IIIF.Presentation.V3;
using Models.DLCS;

namespace Repository.Paths;

/// <summary>
/// Helper class for parsing paths to extract elements (identifiers most likely)
/// </summary>
public static class PathParser
{
    private const char PathSeparator = '/';
    
    /// <summary>
    /// Get <see cref="AssetId"/> from a NamedQuery <see cref="Canvas"/>
    /// </summary>
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

    /// <summary>
    /// DLCS mints inline annotation ids as <c>/adjunct-annotations/{customer}/{space}/{asset}</c>. This returns the id
    /// as <c>/annotations/{customer}/{asset}</c>, keeping the host and anything after the asset. Ids that aren't
    /// adjunct annotation ids for <paramref name="assetId"/>'s customer and space are returned unchanged.
    /// </summary>
    public static string? GetAdjunctAnnotationIdWithoutSpace(string? id, AssetId assetId)
    {
        if (id == null) return null;

        var adjunctPath = $"/adjunct-annotations/{assetId.Customer}/{assetId.Space}/";
        var index = id.IndexOf(adjunctPath, StringComparison.Ordinal);
        if (index < 0) return id;

        return string.Concat(id.AsSpan(0, index), $"/annotations/{assetId.Customer}/",
            id.AsSpan(index + adjunctPath.Length));
    }
}
