using API.Infrastructure.Requests;
using IIIF.Presentation;
using Models.API.Collection;

namespace API.Features.Storage.Helpers;

/// <summary>
/// Builds the plain-IIIF response shared by the hierarchical Collection POST/PUT handlers.
/// </summary>
public static class HierarchicalCollectionResponse
{
    /// <summary>
    /// Guards a <see cref="CollectionWriteService"/> result and, on success, builds the hierarchical response: a
    /// minimal placeholder for storage collections, or the client's own submitted body (re-parsed) for IIIF
    /// collections - preserving any custom behaviors that the write service's enriched entity would otherwise have
    /// discarded (see <see cref="API.Converters.CollectionConverter.EnrichPresentationCollection"/>).
    /// </summary>
    /// <param name="result">Result of the underlying <see cref="ICollectionWrite"/> call</param>
    /// <param name="rawRequestBody">The raw request body, re-parsed for non-storage collections</param>
    /// <param name="isStorageCollection">Whether the written resource is a storage collection</param>
    /// <param name="hierarchicalId">The hierarchical id to set on the response</param>
    /// <param name="logger">Logger, used for re-parse warnings</param>
    public static PresentationResult Build(PresentationResult result, string rawRequestBody,
        bool isStorageCollection, string hierarchicalId, ILogger logger)
    {
        if (!result.IsSuccess || result.Entity is not PresentationCollection presentationCollection) return result;

        IIIF.Presentation.V3.Collection responseCollection;
        if (isStorageCollection)
        {
            responseCollection = new IIIF.Presentation.V3.Collection { Label = presentationCollection.Label, Items = [] };
            responseCollection.EnsurePresentation3Context();
        }
        else
        {
            responseCollection = rawRequestBody.ConvertCollectionToIIIF(logger).ConvertedIIIF!;
        }

        responseCollection.Id = hierarchicalId;

        return PresentationResult.Success(responseCollection, result.WriteResult, result.ETag);
    }
}
