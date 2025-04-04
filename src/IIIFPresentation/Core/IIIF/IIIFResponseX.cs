using IIIF;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Core.IIIF;

public static class IIIFResponseX
{
    /// <summary>
    /// Convert provided stream to Presentation type inheriting from <see cref="JsonLdBase"/>
    /// </summary>
    /// <remarks>
    /// There is a slight difference between the IIIF ToJson method, in that ToPresentation is case-insensitive
    /// </remarks>
    public static async Task<T?> ToPresentation<T>(this Stream contentStream, JsonSerializerSettings? settings = null,
        ILogger? logger = null)
        where T : JsonLdBase, new()
    {
        using var streamReader = new StreamReader(contentStream);
        return await DeserializeStream<T>(settings, streamReader, logger);
    }
    
    /// <summary>
    /// Convert provided string to Presentation type inheriting from <see cref="JsonLdBase"/>
    /// </summary>
    /// <remarks>
    /// There is a slight difference between the IIIF ToJson method, in that ToPresentation is case-insensitive
    /// </remarks>
    public static async Task<T?> ToPresentation<T>(this string content, JsonSerializerSettings? settings = null,
        ILogger? logger = null)
        where T : JsonLdBase, new()
    {
        using var streamReader = new StringReader(content);
        return await DeserializeStream<T>(settings, streamReader, logger);
    }
    
    private static async Task<T?> DeserializeStream<T>(JsonSerializerSettings? settings, TextReader streamReader,
        ILogger? logger = null)
        where T : JsonLdBase, new()
    {
        await using var jsonReader = new JsonTextReader(streamReader);

        settings ??= new(IIIFSerialiserX.DeserializerSettings);

        var serializer = JsonSerializer.Create(settings);

        try
        {
            var result = new T();
            serializer.Populate(jsonReader, result);
            return result;
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "An error occurred while deserializing the presentation request");
            return null;
        }
    }
}
