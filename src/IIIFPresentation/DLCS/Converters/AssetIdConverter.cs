using System.Text.Json;
using System.Text.Json.Serialization;
using Models.DLCS;

namespace DLCS.Converters;

public class AssetIdConverter : JsonConverter<AssetId>
{
    public override AssetId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => AssetId.FromString(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, AssetId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
