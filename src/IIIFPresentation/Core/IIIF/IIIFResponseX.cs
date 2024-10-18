using IIIF;
using IIIF.Serialisation;
using Newtonsoft.Json;

namespace Core.IIIF;

public static class IIIFResponseX
{
    /// <summary>
    /// Convert provided stream to Presentation type inheriting from <see cref="JsonLdBase"/>
    /// </summary>
    public static async Task<T?> ToPresentation<T>(this Stream contentStream, JsonSerializerSettings? settings = null)
        where T : JsonLdBase, new()
    {
        using var streamReader = new StreamReader(contentStream);
        await using var jsonReader = new JsonTextReader(streamReader);

        settings ??= new(IIIFSerialiserX.DeserializerSettings);

        var serializer = JsonSerializer.Create(settings);

        var result = new T();
        serializer.Populate(jsonReader, result);
        return result;
    }
}