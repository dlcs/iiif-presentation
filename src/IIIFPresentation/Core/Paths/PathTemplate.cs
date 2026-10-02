using System.ComponentModel;
using System.Globalization;
using Core.Web;

namespace Core.Paths;

/// <summary>
/// A presentation path template, containing {Escaped} slugs (e.g. {customerId}, {resourceId}) that can be replaced
/// to generate a concrete path.
/// </summary>
/// <remarks>
/// Carries a <see cref="TypeConverter"/> so the configuration binder can bind string values from appsettings/env vars
/// to this type. There is an implicit conversion from <see cref="string"/>, but not the reverse - use
/// <see cref="Template"/> to access the raw template string.
/// </remarks>
[TypeConverter(typeof(PathTemplateConverter))]
public readonly record struct PathTemplate(string Template)
{
    public static class SupportedTemplateOptions
    {
        public static string HierarchyPath => "hierarchyPath";
        public static string ResourceId => "resourceId";
        public static string CustomerId => "customerId";
    }
    
    private const char PathSeparator = '/';

    public static implicit operator PathTemplate(string template) => new(template);

    /// <summary>
    /// Split the raw template into its individual path segments, removing empty entries and trimming whitespace.
    /// </summary>
    /// <returns>The template's path segments (e.g. ["{customerId}", "manifests", "{resourceId}"])</returns>
    public string[] TemplateParts() =>
        Template.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Replace known slugs in this path template.
    /// </summary>
    /// <param name="customer">Value to replace {customerId} with</param>
    /// <param name="hierarchyPath">Value to replace {hierarchyPath} with</param>
    /// <param name="resourceId">Value to replace {resourceId} with</param>
    /// <returns>Template with string replacements made</returns>
    public string GeneratePath(int? customer = null, string? hierarchyPath = null, string? resourceId = null)
    {
        return Template
            .Replace($"{{{SupportedTemplateOptions.CustomerId}}}", customer?.ToString() ?? string.Empty)
            .Replace($"{{{SupportedTemplateOptions.HierarchyPath}}}", hierarchyPath?.TrimStart('/') ?? string.Empty)
            .Replace($"{{{SupportedTemplateOptions.ResourceId}}}", resourceId ?? string.Empty)
            .TrimEnd('/');
    }

    public override string ToString() => Template;
}

/// <summary>
/// <see cref="TypeConverter"/> that allows the configuration binder to convert string values into
/// <see cref="PathTemplate"/> when binding <see cref="TypedPathTemplateOptions"/>.
/// </summary>
public class PathTemplateConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string s ? new PathTemplate(s) : base.ConvertFrom(context, culture, value);
}
