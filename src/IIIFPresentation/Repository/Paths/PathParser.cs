﻿using Core.Helpers;
using IIIF.Presentation.V3;
using Models.API.Manifest;
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

    public static string GetCanvasId(CanvasPainting canvasPainting, int customerId)
    {
        var canvasId = canvasPainting.CanvasId.ThrowIfNull(nameof(canvasPainting));

        if (!Uri.IsWellFormedUriString(canvasId, UriKind.Absolute))
        {
            CheckForProhibitedCharacters(canvasId);

            return canvasId;
        }

        var convertedCanvasId = new Uri(canvasId).PathAndQuery;
        var startsWith = $"/{customerId}/canvases/";

        if (!convertedCanvasId.StartsWith(startsWith) || convertedCanvasId.Length == startsWith.Length)
            throw new ArgumentException($"Canvas Id {convertedCanvasId} is not valid");

        var actualCanvasId =
            convertedCanvasId.Substring(startsWith.Length, convertedCanvasId.Length - startsWith.Length);
        CheckForProhibitedCharacters(actualCanvasId);

        return actualCanvasId;
    }

    private static void CheckForProhibitedCharacters(string canvasId)
    {
        if (prohibitedCharacters.Any(pc => canvasId.Contains(pc)))
        {
            throw new ArgumentException($"canvas Id {canvasId} contains a prohibited character");
        }
    }

    public static string GetHierarchicalFullPathFromPath(string presentationParent, int customerId) =>
        presentationParent.Trim('/').TrimExpect($"{customerId}").Trim('/');

    /// <summary>
    ///     Will ensure <paramref name="input" /> starts with entire <paramref name="expectation" />
    ///     but will omit it from output. Throws if strings differ.
    /// </summary>
    /// <param name="input">a string</param>
    /// <param name="expectation">string of characters expected to be present from <paramref name="input" /></param>
    /// <returns><paramref name="input" /> with the <paramref name="expectation" /> omitted from the start</returns>
    /// <exception cref="FormatException">
    ///     if the <paramref name="input" /> does not start with <paramref name="expectation" />
    /// </exception>
    private static string TrimExpect(this string input, string expectation)
    {
        if (expectation.Length <= 0) return input;
        var i = 0;
        while (i < expectation.Length)
        {
            if (input[i] != expectation[i])
                throw new FormatException($"Expected character '{expectation[i]}' but found '{input[i]}' at index {i}");
            ++i;
        }

        return input[i..];
    }
    public static Uri GetParentUriFromPublicId(string publicId) => 
        new(publicId[..publicId.LastIndexOf('/')]);

    private static readonly List<char> prohibitedCharacters =
    [
        '/',
        '=',
        '=',
        ',',
    ];
}

