using API.Features.Storage.Models;
using Core.IIIF;
using IIIF;
using IIIF.Serialisation;
using Models.API;

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
    public static TryConvertIIIFResult<T> ConvertCollectionToIIIF<T>(this string requestBody, ILogger logger)
        where T : JsonLdBase
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
    /// Attempts to deserialize a <see cref="IPresentation"/> resource.
    ///
    /// As this populates an <see cref="IPresentation"/> resource it needs to be instantiated prior to population, which
    /// leads to this method being more forgiving than <see cref="ConvertCollectionToIIIF{T}"/>
    /// </summary>
    /// <param name="requestBody">The raw request body to convert</param>
    /// <param name="logger"></param>
    /// <returns>A result containing the deserialized resource, or a failure</returns>
    public static async Task<TryConvertIIIFResult<T>> TryDeserializePresentation<T>(this string requestBody,
        ILogger? logger = null) 
        where T : JsonLdBase, IPresentation, new()
    {
        try
        {
            var presentation = await requestBody.ToPresentation<T>();
            
            return presentation == null
                ? TryConvertIIIFResult<T>.Failure()
                : TryConvertIIIFResult<T>.Success(presentation);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "An error occurred while attempting to deserialize");
            return TryConvertIIIFResult<T>.Failure();
        }
    }
}
