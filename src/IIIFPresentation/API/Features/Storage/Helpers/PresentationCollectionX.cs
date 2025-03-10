using Core.Helpers;
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
    /// The id of thumbnail that is closest preconfigured size
    /// </returns>
    public static string? GetThumbnail(this PresentationCollection collection)
    {
        var thumbnails = collection.Thumbnail?.OfType<Image>().ToList();
        return thumbnails.IsNullOrEmpty() ? null : thumbnails.GetThumbnailPath();
    }
}
