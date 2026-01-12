using System.Text.RegularExpressions;
using Core.Web;
using Microsoft.Extensions.Options;
using Models.API.General;
using static Core.Web.TypedPathTemplateOptions;

namespace Repository.Paths;

public interface IPathRewriteParser
{
    public PathParts ParsePathWithRewrites(string host, string path, int customer);
    
    public PathParts ParsePathWithRewrites(string? uri, int customer);
}

public class PathRewriteParser(IOptions<TypedPathTemplateOptions> options, ILogger<PathRewriteParser> logger)
    : IPathRewriteParser
{
    private readonly TypedPathTemplateOptions settings = options.Value;

    private const char PathSeparator = '/';

    /// <summary>
    /// Parses a full URI into  the required path segments, taking into account path rewrites
    /// </summary>
    /// <param name="host"></param>
    /// <param name="path">The path to match against</param>
    /// <param name="customer">The customer ID to use in the case that the path doesn't have one</param>
    public PathParts ParsePathWithRewrites(string host, string path, int customer)
    {
        // Always try and parse canonical first
        var canonical = ParseCanonical(path);
        if (canonical != null) return canonical;

        // Not canonical - try and match to a path...
        var templates = settings.GetPathTemplatesForHost(host);

        // First split the path into it's individual segments
        var pathSplit = path.Split(PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var template in templates)
        {
            // Split template into chunks
            var templateSplit = template.Value.Split(PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // work out if the template is a FQDN and remove the host if it is
            if (Uri.TryCreate(template.Value, UriKind.Absolute, out var uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                templateSplit = templateSplit.Skip(2).ToArray();
            }

            // Check lengths are same, if not don't compare, or it's possible to be just the host value
            if (pathSplit.Length != templateSplit.Length &&
                template.Key != PresentationResourceType.ResourcePublic) continue;

            try
            {
                var (customerId, resourceId) =
                    MatchValuesInTemplate(pathSplit, templateSplit, customer);
                if (resourceId != null)
                {
                    return new PathParts(customerId, resourceId,
                        template.Key == PresentationResourceType.ResourcePublic);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while parsing path {Path}, with template {Template}", path, template);
            }
        }

        return new PathParts(null, null, true);
    }

    public PathParts ParsePathWithRewrites(string? uri, int customer)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var uriResult))
        {
            return ParsePathWithRewrites(uriResult.Host, uriResult.AbsolutePath, customer);
        }

        return new PathParts(null, null, true);;
    }

    private static (int customerId, string? resourceId) MatchValuesInTemplate(string[] pathSplit,
        string[] templateSplit, int customer)
    {
        int? customerIdFromPath = null;
        string? resourceId = null;

        for (var i = 0; i < pathSplit.Length; i++)
        {
            var templatePart = templateSplit[i];
            var valuePart = pathSplit[i];
            
            var match = GeneratedRegexes.ReplacementRegex().Match(templatePart);
            // Check if this is a replacement value - if so get value from provided path 
            if (match.Success)
            {
                // This is a template - get the value of it from the path value
                var capturedValue = match.Groups[1].Value;
                if (capturedValue == SupportedTemplateOptions.CustomerId)
                {
                    customerIdFromPath = int.Parse(valuePart);
                }
                else if (capturedValue == SupportedTemplateOptions.ResourceId)
                {
                    resourceId = valuePart;
                }
                else if (capturedValue == SupportedTemplateOptions.HierarchyPath &&
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
            templateSplit.Contains($"{{{SupportedTemplateOptions.HierarchyPath}}}") &&
            resourceId == null)
        {
            resourceId = string.Empty;
        }

        return (customerIdFromPath ?? customer, resourceId);
    }

    private PathParts? ParseCanonical(string path)
    {
        var match = GeneratedRegexes.CanonicalRegex().Match(path);

        if (!match.Success) return null;

        logger.LogTrace("{Path} is a canonical path", path);
        var customer = int.Parse(match.Groups[1].Value);

        return new PathParts(customer, match.Groups[3].Value, false);
    }
}

public record PathParts(int? Customer, string? Resource, bool Hierarchical);

internal partial class GeneratedRegexes
{
    [GeneratedRegex("^{(.+)}$")]
    internal static partial Regex ReplacementRegex();
    
    /// <note>
    /// The string values in this regex should match the values found in <see cref="SpecConstants"/>
    /// </note>
    [GeneratedRegex("^\\/?(\\d+)\\/(manifests|collections|canvases)\\/(.+)$")]
    internal static partial Regex CanonicalRegex();
}
