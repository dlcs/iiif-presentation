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

    /// <summary>
    /// Parses a full URI into  the required path segemnts, taking into account path rewrites
    /// </summary>
    /// <param name="settings"><see cref="TypedPathTemplateOptions"/> used to grab templates from></param>
    /// <param name="host"></param>
    /// <param name="path">The path to match agains</param>
    /// <param name="customer">The customer ID to use in the case that the path doesn't have one</param>
    /// <param name="logger">logger for errors</param>
    public static PathParts ParsePathWithRewrites(TypedPathTemplateOptions settings, string host, string path, int customer, ILogger logger)
    {
        var templates = settings.GetPathTemplatesForHost(host);
            
	    var replacementRegex = new Regex("^{(.+)}$");
        
        // Always try and parse canonical first
        var canonical = ParseCanonical(path, customer);
        if (canonical != null) return canonical;
        
	    // Not canonical - try and match to a path...
	    // First split the path into it's individual segments
	    var pathSplit = path.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		    
	    foreach (var template in templates)
	    {
		    // Split template into chunks
		    var templateSplit = template.Value.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // work out if the template is a FQDN and remove the host if it is
            if  (templateSplit.First().Contains("http")) templateSplit = templateSplit.Skip(2).ToArray();
		    
		    // Check lengths are same, if not don't compare, or it's possible to be just the host value
		    if (pathSplit.Length != templateSplit.Length && template.Key != PresentationResourceType.ResourcePublic) continue;

            try
            {
                var (customerId, resourceId) =
                    MatchValuesInTemplate(pathSplit, templateSplit, replacementRegex, customer);
                if (resourceId != null)
                {
                    return new PathParts(customerId, resourceId, template.Key != PresentationResourceType.ResourcePublic);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while parsing path {Path}", path);
            }
        }
	    
	    return new PathParts(null, null, false);
    }

    private static (int customerId, string? resourceId) MatchValuesInTemplate(string[] pathSplit, string[] templateSplit, Regex replacementRegex, int customer)
    {
        int? customerIdFromPath = null;
        string? resourceId = null;

        for (var i = 0; i < pathSplit.Length; i++)
        {
            var templatePart = templateSplit[i];
            var valuePart = pathSplit[i];
			    
            // Check if this is a replacement value - if so get value from provided path 
            if (replacementRegex.IsMatch(templatePart))
            {
                // This is a template - get the value of it from the path value
                var capturedValue = replacementRegex.Match(templatePart).Groups[1].Value;
                if (capturedValue == TypedPathTemplateOptions.SupportedTemplateOptions.CustomerId)
                {
                    customerIdFromPath = int.Parse(valuePart);
                }
                else if (capturedValue == TypedPathTemplateOptions.SupportedTemplateOptions.ResourceId) resourceId = valuePart;
                else if (capturedValue == TypedPathTemplateOptions.SupportedTemplateOptions.HierarchyPath && 
                         !SpecConstants.ProhibitedSlugs.Contains(valuePart))
                {
                    // everything in the path after hierarchy goes into the path
                    resourceId = string.Join(PathSeparator, pathSplit.Skip(i));
                    break;
                }
            }
            else if (templatePart != valuePart)
            {
                // if this isn't a path replacement and the values don't match, abort
                break;
            }
        }

        // if the length is 1 less, and the template split is hierarchical, it means the root collection
        if (pathSplit.Length == templateSplit.Length - 1 &&
            templateSplit.Contains($"{{{TypedPathTemplateOptions.SupportedTemplateOptions.HierarchyPath}}}") &&
            resourceId == null)
        {
            resourceId = string.Empty;
        }

        return (customerIdFromPath ?? customer, resourceId);
    }
    
    private static PathParts? ParseCanonical(string path, int customer)
    {
        var canonicalRegex = new Regex("^\\/?(\\d+?)\\/(manifests|collections|canvases)\\/([\\w\\d]+)$");

        if (!canonicalRegex.IsMatch(path)) return null;
	
        var matches = canonicalRegex.Match(path);

        if (matches.Groups[1] != null)
        {
            customer = int.Parse(matches.Groups[1].Value);
        }
        
        return new PathParts(customer, matches.Groups[3].Value.ToString(), true);
    }

    public record PathParts(int? Customer, string? Resource, bool Canonical);

    public static Uri GetParentUriFromPublicId(string publicId) => 
        new(publicId[..publicId.LastIndexOf(PathSeparator)]);

    private static readonly List<char> ProhibitedCharacters = ['/', '=', '=', ',',];
    private static string ProhibitedCharacterDisplay = string.Join(',', ProhibitedCharacters.Select(p => $"'{p}'"));

}
