using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Services.TextServices;

/// <summary>
/// What a completed text-services job reported, stored as the result of the text pipeline job
/// </summary>
public class TextPipelineResult
{
    public const string NoTextFoundWarning = "No text resources found";
    public const string PageErrorsWarning = "Errors were reported for some pages, see result for details";

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    });

    /// <summary>
    /// Total pages, as reported by text-services
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Total words indexed, as reported by text-services
    /// </summary>
    public int TotalWordCount { get; init; }

    /// <summary>
    /// Skipped canvases, as reported by text-services
    /// </summary>
    public int SkippedCanvases { get; init; }

    /// <summary>
    /// Pages without a text source, as reported by text-services
    /// </summary>
    public int PagesWithoutTextSource { get; init; }

    /// <summary>
    /// Pages without words, as reported by text-services
    /// </summary>
    public int PagesWithoutWords { get; init; }

    /// <summary>
    /// Errors, as reported by text-services
    /// </summary>
    public List<string>? Errors { get; init; }

    /// <param name="errors">Newline-separated errors, split into <see cref="Errors"/></param>
    public static TextPipelineResult Create(int totalPages, int totalWordCount, string? errors,
        int skippedCanvases = 0, int pagesWithoutTextSource = 0, int pagesWithoutWords = 0)
    {
        var errorList = errors?
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        return new TextPipelineResult
        {
            TotalPages = totalPages,
            TotalWordCount = totalWordCount,
            SkippedCanvases = skippedCanvases,
            PagesWithoutTextSource = pagesWithoutTextSource,
            PagesWithoutWords = pagesWithoutWords,
            Errors = errorList?.Count > 0 ? errorList : null
        };
    }

    /// <summary>
    /// <see cref="NoTextFoundWarning"/> if no words were indexed, <see cref="PageErrorsWarning"/> if there are
    /// errors, otherwise null
    /// </summary>
    public string? GetWarning()
    {
        if (TotalWordCount == 0) return NoTextFoundWarning;
        return Errors == null ? null : PageErrorsWarning;
    }

    public JObject ToJObject() => JObject.FromObject(this, Serializer);
}
