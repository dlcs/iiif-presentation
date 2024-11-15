using System.Runtime.Serialization;
using IIIF;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Models.API.Collection;
using Models.API.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core.IIIF;

public static class IIIFResponseX
{
    /// <summary>
    /// Convert provided stream to Presentation type inheriting from <see cref="JsonLdBase"/>
    /// </summary>
    /// <remarks>
    /// There is a slight difference between the IIIF ToJson method, in that ToPresentation is case-insensitive
    /// </remarks>
    public static async Task<T?> ToPresentation<T>(this Stream contentStream, JsonSerializerSettings? settings = null)
        where T : JsonLdBase, new()
    {
        using var streamReader = new StreamReader(contentStream);
        return await DeserializeStream<T>(settings, streamReader);
    }
    
    /// <summary>
    /// Convert provided string to Presentation type inheriting from <see cref="JsonLdBase"/>
    /// </summary>
    /// <remarks>
    /// There is a slight difference between the IIIF ToJson method, in that ToPresentation is case-insensitive
    /// </remarks>
    public static async Task<T?> ToPresentation<T>(this string content, JsonSerializerSettings? settings = null)
        where T : JsonLdBase, new()
    {
        using var streamReader = new StringReader(content);
        return await DeserializeStream<T>(settings, streamReader);
    }
    
    private static async Task<T?> DeserializeStream<T>(JsonSerializerSettings? settings, TextReader streamReader)
        where T : JsonLdBase, new()
    {
        await using var jsonReader = new JsonTextReader(streamReader);

        settings ??= new(IIIFSerialiserX.DeserializerSettings);
        settings.Context  = new StreamingContext(StreamingContextStates.Other,
            new Dictionary<Type, IDictionary<string, Func<JObject, object>>>
            {
                { 
                    typeof(ICollectionItem), 
                    new Dictionary<string, Func<JObject, object>>
                    {
                        { "Collection", p => new PresentationCollectionItem() }, 
                        { "Manifest", p => new PresentationManifestItem() } 
                    }
                }
            });

        var serializer = JsonSerializer.Create(settings);

        try
        {
            var result = new T();
            serializer.Populate(jsonReader, result);
            return result;
        }
        catch (JsonException)
        {
            return default;
        }
    }
}