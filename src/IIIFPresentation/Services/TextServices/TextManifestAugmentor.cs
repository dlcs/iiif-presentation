using Core.Helpers;
using Core.IIIF;
using IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using IIIF.Search.V2;
using Microsoft.Extensions.Logging;
using Services.Manifests;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.TextServices;

/// <summary>
/// Augments a manifest with text-services content - retrieving the text-augmented manifest from text-services and
/// merging any <see cref="SearchService2"/> into the manifest.
/// </summary>
public interface ITextManifestAugmentor : IManifestAugmentor
{
}

public class TextManifestAugmentor(ITextSearchClient textSearchClient, ILogger<TextManifestAugmentor> logger)
    : ITextManifestAugmentor
{
    public async Task<Manifest> Augment(Manifest manifest, DbManifest dbManifest, CancellationToken cancellationToken)
    {
        // The job id is deterministic from the manifest - see PipelineJobX.GetJobId
        var jobId = new TextJobId(dbManifest.CustomerId, dbManifest.Id);

        var augmented = await textSearchClient.GetTextAugmentedManifest(jobId, cancellationToken);

        var searchServices = augmented?.Service?.OfType<SearchService2>().ToList();
        if (searchServices.IsNullOrEmpty())
        {
            logger.LogDebug("No SearchService2 in text-augmented manifest for job {JobId}", jobId);
            return manifest;
        }

        // Add search service to manifest, if added then ensure Manifest has the search context
        manifest.Service ??= [];
        var added = manifest.Service.AddDistinctById(searchServices, AddService);
        if (added > 0)
        {
            manifest.EnsureContext(SearchService2.Search2Context);
            logger.LogDebug("Added SearchService2 to manifest for job {JobId}", jobId);
        }
        else
        {
            logger.LogDebug("Found SearchService2 but did not augment manifest for job {JobId}", jobId);
        }

        return manifest;
    }

    private static void AddService(IService service)
    {
        // Expectation is we'll get a SearchService2 containing an AutoCompleteService2. Set labels on these if null
        if (service is SearchService2 searchService)
        {
            searchService.Label ??= new LanguageMap("en", "Search within this manifest");
            // We're only expecting 1 here but use FirstOrDefault, rather than SingleOrDefault to avoid throwing if
            // text-service adds unexpected service.
            var autoComplete = searchService.Service?.OfType<AutoCompleteService2>().FirstOrDefault();
            if (autoComplete != null)
            {
                autoComplete.Label ??= new LanguageMap("en", "Autocomplete words in this manifest");
            }
        }
    }
}
