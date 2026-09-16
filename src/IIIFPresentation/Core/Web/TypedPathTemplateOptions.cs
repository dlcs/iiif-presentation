using Core.Paths;

namespace Core.Web;

/// <summary>
/// A collection of options related to path generation.
/// </summary>
public class TypedPathTemplateOptions
{
    public const string SettingsName = "PathRules";

    private static readonly Dictionary<string, PathTemplate> DefaultFormats = new ()
    {
        ["ManifestPrivate"] = "/{customerId}/manifests/{resourceId}",
        ["CollectionPrivate"] = "/{customerId}/collections/{resourceId}",
        ["ResourcePublic"] = "/{customerId}/{hierarchyPath}",
        ["Canvas"] = "/{customerId}/canvases/{resourceId}",
        ["TextServiceJob"] = "/{customerId}/iiif/{resourceId}",
    };

    /// <summary>
    /// Types that have no default template of their own - when not explicitly overridden for a host, they fall
    /// back to that host's "TextServiceJob" template (host override if set, else its default)
    /// </summary>
    private static readonly Dictionary<string, string> FallbackTypes = new()
    {
        ["TextServiceSearchService"] = "TextServiceJob",
        ["TextServiceRendering"] = "TextServiceJob",
        ["TextServiceAnnotations"] = "TextServiceJob",
    };

    /// <summary>
    /// Default path names for the different types of path
    /// </summary>
    public Dictionary<string, PathTemplate> Defaults { get; set; } = new(DefaultFormats);

    /// <summary>
    /// Collection of path template overrides, these are keyed by "hostname" and sub-dictionary keyed by type
    /// </summary>
    public Dictionary<string, Dictionary<string, PathTemplate>> Overrides { get; set; } = new();

    /// <summary>
    /// Get all templates for host.
    /// </summary>
    /// <returns>Paths for host, or defaults if override not found.</returns>
    public Dictionary<string, PathTemplate> GetPathTemplatesForHost(string host) => Defaults
        .ToDictionary(format => format.Key, format => GetPathTemplateForHostAndType(host, format.Key));

    /// <summary>
    /// Get template path for host.
    /// </summary>
    /// <param name="host">Host to get template path for.</param>
    /// <param name="type">Type of item to get template path for.</param>
    /// <returns>
    /// Host-specific override if set; else that type's default if set; else (for a type in
    /// <see cref="FallbackTypes"/>) the resolved "TextServiceJob" template for the same host.
    /// </returns>
    public PathTemplate GetPathTemplateForHostAndType(string host, string type)
    {
        if (Overrides.TryGetValue(host, out var hostLevel) && hostLevel.TryGetValue(type, out var hostTemplate))
        {
            return hostTemplate;
        }

        if (Defaults.TryGetValue(type, out var template))
        {
            return template;
        }

        if (FallbackTypes.TryGetValue(type, out var fallbackType))
        {
            return GetPathTemplateForHostAndType(host, fallbackType);
        }

        throw new KeyNotFoundException($"Could not find default path template for type: {type}");
    }
}
