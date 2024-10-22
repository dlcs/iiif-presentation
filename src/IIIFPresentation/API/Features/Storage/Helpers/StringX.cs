using IIIF.Presentation.V3.Content;
using IIIF.Serialisation;
using Models.Database.Collections;

namespace API.Features.Storage.Helpers;

public static class StringX
{
    /// <summary>
    /// Converts a raw request body into a IIF collection
    /// </summary>
    /// <param name="requestBody">The request body to convert</param>
    /// <param name="collection">The collection to set the thumbnail for</param>
    /// <returns>A string containing the valid IIIF collection</returns>
    private static string ConvertToIIIFCollectionAndSetThumbnail(this string requestBody, Collection collection)
    {
        var collectionAsIIIF = requestBody.FromJson<IIIF.Presentation.V3.Collection>();
        var convertedIIIFCollection = collectionAsIIIF.AsJson();
        var thumbnails = collectionAsIIIF.Thumbnail?.OfType<Image>().ToList();
        if (thumbnails != null)
        {
            collection.Thumbnail = thumbnails.GetThumbnailPath();
        }
        return convertedIIIFCollection;
    }
    
    /// <summary>
    /// Converts a collection to IIIF and sets the thumbnail for a collection based on the conversion
    /// </summary>
    /// <param name="requestBody">The body of a request, used for conversion</param>
    /// <param name="collection">The collection to set a thumbnail for</param>
    /// <param name="presentationThumbnail">The fallback value for a thumbnail</param>
    /// <param name="logger">The logger</param>
    /// <returns>
    /// A response showing whether there were errors in the conversion, and a string of the converted collection
    /// </returns>
    public static ConvertedIIIF ConvertToIIIFAndSetThumbnail(this string requestBody,
        Collection collection, string? presentationThumbnail, ILogger logger)
    {
        var convertedIIIFCollection = string.Empty;
        
        if (!collection.IsStorageCollection)
        {
            try
            {
                convertedIIIFCollection = requestBody.ConvertToIIIFCollectionAndSetThumbnail(collection);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while attempting to validate the collection as IIIF");
                return new ConvertedIIIF(true, convertedIIIFCollection);
            }
        }
        else
        {
            collection.Thumbnail = presentationThumbnail;
        }
        return new ConvertedIIIF(false, convertedIIIFCollection);
    }
}

public record ConvertedIIIF(
    bool Error,
    string ConvertedCollection);