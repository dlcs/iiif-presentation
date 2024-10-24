using API.Features.Storage.Models;
using IIIF.Presentation.V3.Content;
using IIIF.Serialisation;
using Models.Database.Collections;

namespace API.Features.Storage.Helpers;

public static class StringX
{
    /// <summary>
    /// Converts a request body into a collection
    /// </summary>
    /// <param name="requestBody">The body of a request, used for conversion</param>
    /// <param name="logger">The logger</param>
    /// <returns>
    /// A response showing whether there were errors in the conversion, and a string of the converted collection
    /// </returns>
    public static TryConvertIIIFResult<IIIF.Presentation.V3.Collection> ConvertCollectionToIIIF(this string requestBody, ILogger logger)
    {
        try
        {
            var collection = requestBody.FromJson<IIIF.Presentation.V3.Collection>();
            return TryConvertIIIFResult<IIIF.Presentation.V3.Collection>.Success(collection);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while attempting to validate the collection as IIIF");
            return TryConvertIIIFResult<IIIF.Presentation.V3.Collection>.Failure();
        }
    }
}