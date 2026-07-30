using IIIF.Presentation;
using IIIF.Presentation.V3.Strings;

namespace API.Features.Storage.Helpers;

/// <summary>
/// Builds the plain-IIIF response body shared by the hierarchical Collection POST/PUT handlers.
/// </summary>
public static class HierarchicalCollectionResponse
{
    /// <summary>
    /// Builds the response: a minimal placeholder for storage collections, or the client's own submitted body
    /// (re-parsed) for IIIF collections - preserving any custom behaviors that the write service's enriched entity
    /// would otherwise have discarded (see <see cref="API.Converters.CollectionConverter.EnrichPresentationCollection"/>).
    /// </summary>
    /// <param name="isStorageCollection">Whether the written resource is a storage collection</param>
    /// <param name="rawRequestBody">The raw request body, re-parsed for non-storage collections</param>
    /// <param name="label">Label to use for the storage-collection placeholder</param>
    /// <param name="hierarchicalId">The hierarchical id to set on the response</param>
    /// <param name="logger">Logger, used for re-parse warnings</param>
    public static IIIF.Presentation.V3.Collection Build(bool isStorageCollection, string rawRequestBody,
        LanguageMap? label, string hierarchicalId, ILogger logger)
    {
        IIIF.Presentation.V3.Collection responseCollection;
        if (isStorageCollection)
        {
            responseCollection = new IIIF.Presentation.V3.Collection { Label = label, Items = [] };
            responseCollection.EnsurePresentation3Context();
        }
        else
        {
            responseCollection = rawRequestBody.ConvertCollectionToIIIF(logger).ConvertedIIIF!;
        }

        responseCollection.Id = hierarchicalId;

        return responseCollection;
    }
}
