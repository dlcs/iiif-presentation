﻿using Core.Helpers;
using IIIF.Presentation.V3;
using Models.DLCS;

namespace Repository.Paths;

/// <summary>
/// Helper class for parsing paths to extract elements (identifiers most likely)
/// </summary>
public static class PathParser
{
    public static AssetId GetAssetIdFromNamedQueryCanvasId(this Canvas canvas, ILogger? logger = null)
    {
        var canvasId = canvas.Id.ThrowIfNullOrWhiteSpace(nameof(canvas.Id));

        try
        {
            var assetParts =
                canvasId[..canvasId.IndexOf("/canvas/c/", StringComparison.OrdinalIgnoreCase)].Split("/")[^3..];
            return AssetId.FromString(string.Join('/', assetParts));
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Error while parsing NQ canvas Id {CanvasId}", canvas.Id);
            throw new FormatException($"Unable to extract AssetId from {canvas.Id}");
        }
    }
    
    public static string? GetCanvasId(Models.API.Manifest.CanvasPainting? canvasPainting, int customerId)
    {
        var canvasId = canvasPainting?.CanvasId;
        
        if (canvasId == null || !Uri.IsWellFormedUriString(canvasId, UriKind.Absolute)) return canvasId;
        
        var convertedCanvasId = new Uri(canvasId).PathAndQuery;
        var startsWith = $"/{customerId}/canvases/";

        if (!convertedCanvasId.StartsWith(startsWith) || convertedCanvasId.Length == startsWith.Length)
            throw new ArgumentException("Canvas Id is not valid");
        
        return canvasId.GetLastPathElement();
    }
}

