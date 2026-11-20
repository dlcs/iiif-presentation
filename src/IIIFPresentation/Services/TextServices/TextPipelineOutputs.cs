using Models.API.Manifest.Pipelines;
using Models.Database.General;

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
    /// Get the <see cref="JobServices"/> requested by this config. A null "output" is treated as "All", and "All"
    /// wins over any other value. A "pdfCoverPage" always adds <see cref="JobServices.Pdf"/>.
    /// </summary>
    public static JobServices GetRequestedServices(this TextPipelineConfig config)
    {
        if (config.Output == null) return JobServices.All;

        // "All" is itself a JobServices value (~0) so it naturally wins when OR'd with anything else
        var services = config.Output
            .Where(IsValid)
            .Aggregate(JobServices.None, (current, output) => current | Enum.Parse<JobServices>(output, true));

        if (config.PdfCoverPage != null) services |= JobServices.Pdf;
        return services;
    }

    /// <summary>
    /// The text config of this job. A job without one is treated as having the default config.
    /// </summary>
    public static TextPipelineConfig GetTextConfig(this PipelineJob job) =>
        job.GetConfig() as TextPipelineConfig ?? new TextPipelineConfig();

    /// <summary>
    /// The status of a completed job with this config that found <paramref name="totalWordCount"/> words. Without
    /// any words the PDF is the only output that can be produced, so a job that requested more than the PDF has only
    /// partly completed, and one that didn't request the PDF has done nothing.
    /// </summary>
    /// <remarks><see cref="JobServices.TextAugmented"/> isn't counted, as it isn't a text output in its own right</remarks>
    public static PipelineJobStatus GetCompletedStatus(this TextPipelineConfig config, int totalWordCount)
    {
        if (totalWordCount > 0) return PipelineJobStatus.Completed;

        var requested = config.GetRequestedServices() & ~JobServices.TextAugmented;

        if (!requested.HasFlag(JobServices.Pdf)) return PipelineJobStatus.CompletedNoOperation;
        return requested == JobServices.Pdf ? PipelineJobStatus.Completed : PipelineJobStatus.CompletedWithOnlyPdf;
    }
}
