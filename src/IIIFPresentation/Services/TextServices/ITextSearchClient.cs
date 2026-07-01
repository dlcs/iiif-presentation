using IIIF.Presentation.V3;

namespace Services.TextServices;

public interface ITextSearchClient
{
    /// <summary>
    /// Retrieve the text-augmented manifest for a completed job.
    /// Returns null if Manifest is not found or returned a non-2xx status code.
    /// </summary>
    /// <param name="jobId">Job identifier</param>
    Task<Manifest?> GetTextAugmentedManifest(TextJobId jobId, CancellationToken cancellationToken);
}
