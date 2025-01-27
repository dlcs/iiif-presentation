using Core.IIIF;
using IIIF.Presentation.V3.Content;
using Models.API.Collection;

namespace API.Features.Storage.Helpers;

public static class PresentationCollectionX
{
    /// <summary>
    /// Gets the thumbnail from a <see cref="PresentationCollection"/>
    /// </summary>
    /// <param name="collection">The collection to get a thumbnail from</param>
    /// <returns>
    /// A response showing whether there were errors in the conversion, and a string of the converted collection
    /// </returns>
    public static string? GetThumbnail(this PresentationCollection collection)
    {
        if (collection.Thumbnail is List<ExternalResource> thumbnailsAsCollection)
        {
            var thumbnails = thumbnailsAsCollection.OfType<Image>().ToList();
            return thumbnails.GetThumbnailPath();
        }

        return collection.PresentationThumbnail;
    }
}
