using Models.API.Manifest;

namespace Services.TextServices;

public static class TextPipelineOutputs
{
    /// <summary>
    /// Recognised "output" values - "All" plus every <see cref="JobServices"/> flag name.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedValues =
        Enum.GetNames<JobServices>().Where(n => n != nameof(JobServices.None)).ToArray();

    /// <summary>
    /// Whether <paramref name="output"/> is a recognised "output" value - "All" or a <see cref="JobServices"/> flag
    /// name (case-insensitive).
    /// </summary>
    public static bool IsValid(string? output) =>
        AllowedValues.Contains(output, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Get the <see cref="JobServices"/> requested by this config. A null config or "output" is treated as "All",
    /// and "All" wins over any other value. A "pdfCoverPage" always adds <see cref="JobServices.Pdf"/>.
    /// </summary>
    public static JobServices GetRequestedServices(this PipelineConfig? config)
    {
        if (config?.Output == null) return JobServices.All;

        // "All" is itself a JobServices value (~0) so it naturally wins when OR'd with anything else
        var services = config.Output
            .Where(IsValid)
            .Aggregate(JobServices.None, (current, output) => current | Enum.Parse<JobServices>(output, true));

        if (config.PdfCoverPage != null) services |= JobServices.Pdf;
        return services;
    }
}
