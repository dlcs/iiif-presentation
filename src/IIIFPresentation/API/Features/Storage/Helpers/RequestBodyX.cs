using API.Features.Storage.Models;
using Core.IIIF;
using IIIF;
using IIIF.Serialisation;
using Models.API.Collection;

namespace API.Features.Storage.Helpers;

public static class RequestBodyX
{
    /// <summary>
    /// Converts a request body into a collection
    /// </summary>
    /// <param name="requestBody">The body of a request, used for conversion</param>
    /// <param name="logger">The logger</param>
    /// <returns>
    /// A response showing whether there were errors in the conversion, and the converted collection
    /// </returns>
    public static TryConvertIIIFResult<T> ConvertCollectionToIIIF<T>(this string requestBody, ILogger logger) where T : JsonLdBase
    {
        try
        {
            var collection = requestBody.FromJson<T>();
            return TryConvertIIIFResult<T>.Success(collection);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while attempting to validate the collection as IIIF");
            return TryConvertIIIFResult<T>.Failure();
        }
    }
    
    /// <summary>
    /// Attempts to deserialize a presentation collection
    /// </summary>
    /// <param name="requestBody">The raw request body to convert</param>
    /// <returns>A result containing the deserialized collection, or a failure</returns>
    public static async Task<TryConvertIIIFResult<PresentationCollection>> TryDeserializePresentationCollection(this string requestBody) 
    {
        try
        {
            var collection = await requestBody.ToPresentation<PresentationCollection>();
            
            return collection == null
                ? TryConvertIIIFResult<PresentationCollection>.Failure()
                : TryConvertIIIFResult<PresentationCollection>.Success(collection);
        }
        catch (Exception)
        {
            return TryConvertIIIFResult<PresentationCollection>.Failure();
        }
    }
}