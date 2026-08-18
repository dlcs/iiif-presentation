using API.Features.Common.Helpers;
using API.Infrastructure.Requests;
using Core.Helpers;
using IIIF.Presentation;
using Models.API.Collection;
using Services.Manifests.Helpers;

namespace API.Features.Storage.Helpers;

/// <summary>
/// Builds the plain-IIIF response body for a hierarchical Collection write.
/// </summary>
public static class HierarchicalCollectionResponse
{
    /// <summary>
    /// Passes failed or non-collection results through unchanged; on success, builds the hierarchical response: a
    /// minimal placeholder for storage collections, or the client's own submitted body (re-parsed) for IIIF
    /// collections - preserving any custom behaviors that the write service's enriched entity would otherwise have
    /// discarded (see <see cref="API.Converters.CollectionConverter.EnrichPresentationCollection"/>). The response
    /// id is taken from the enriched entity's <c>PublicId</c>, which already accounts for customers with a
    /// configured <see cref="SettingsBasedPathGenerator"/> path.
    /// </summary>
    /// <param name="result">Result of the underlying <see cref="ICollectionWrite"/> call</param>
    /// <param name="rawRequestBody">The raw request body, re-parsed for non-storage collections</param>
    /// <param name="isStorageCollection">Whether the written resource is a storage collection</param>
    /// <param name="logger">Logger, used for re-parse warnings</param>
    public static PresentationResult Build(PresentationResult result, string rawRequestBody,
        bool isStorageCollection, ILogger logger)
    {
        if (!result.IsSuccess || result.Entity is not PresentationCollection presentationCollection) return result;

        IIIF.Presentation.V3.Collection responseCollection;
        if (isStorageCollection)
        {
            responseCollection = new IIIF.Presentation.V3.Collection
            {
                Label = presentationCollection.Label, 
                Items = presentationCollection.Items ?? []
            };
            responseCollection.EnsurePresentation3Context();
        }
        else
        {
            var converted = rawRequestBody.ConvertCollectionToIIIF(logger);
            if (converted.Error) return UpsertErrorHelper.CannotValidateIIIF();

            responseCollection = converted.ConvertedIIIF!;
        }

        responseCollection.Id = presentationCollection.PublicId.ThrowIfNull(nameof(presentationCollection.PublicId));

        return PresentationResult.Success(responseCollection, result.WriteResult, result.ETag);
    }
}
