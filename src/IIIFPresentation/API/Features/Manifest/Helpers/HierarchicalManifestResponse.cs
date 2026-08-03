using API.Converters;
using API.Infrastructure.Requests;
using Core.Helpers;
using Models.API.Manifest;

namespace API.Features.Manifest.Helpers;

/// <summary>
/// Builds the plain-IIIF response body for a hierarchical Manifest write.
/// </summary>
public static class HierarchicalManifestResponse
{
    /// <summary>
    /// Guards a <see cref="ManifestWriteService"/> result and, on success, strips it down to plain IIIF via
    /// <see cref="PresentationIIIFCleaner"/>. The response id is taken from the enriched entity's <c>PublicId</c>
    /// (read before stripping - <c>PublicId</c> is a Presentation-only property, not carried over by the cleaner),
    /// which already accounts for customers with a configured
    /// <see cref="Repository.Paths.SettingsBasedPathGenerator"/> path.
    /// </summary>
    /// <param name="result">Result of the underlying <see cref="IManifestWrite"/> call</param>
    public static PresentationResult Build(PresentationResult result)
    {
        if (!result.IsSuccess || result.Entity is not PresentationManifest manifest) return result;

        var plainManifest = PresentationIIIFCleaner.OnlyIIIFProperties(manifest);
        plainManifest.Id = manifest.PublicId.ThrowIfNull(nameof(manifest.PublicId));

        return PresentationResult.Success(plainManifest, result.WriteResult, result.ETag);
    }
}
