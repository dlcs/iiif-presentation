using API.Converters.Streaming;
using AWS.S3.Models;
using Core.Helpers;
using IIIF;
using IIIF.Serialisation;

namespace API.Converters;

public static class DescriptionResourceConverter
{
    /// <summary>
    /// Deserialized content in <see cref="ObjectFromBucket"/> to provided type and update root "id"
    /// </summary>
    public static T GetDescriptionResourceWithId<T>(this ObjectFromBucket objectFromS3, string replacementId)
        where T : JsonLdBase
    {
        using var memoryStream = new MemoryStream();
        StreamingJsonProcessor.ProcessJson(objectFromS3.Stream.ThrowIfNull(nameof(objectFromS3.Stream)),
            memoryStream,
            objectFromS3.Headers.ContentLength,
            new S3StoredJsonProcessor(replacementId));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream.FromJsonStream<T>();
    }
}
