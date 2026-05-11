using System.Text.Json;
using DLCS.Converters;
using Models.DLCS;

namespace DLCS.Tests.Converters;

public class AssetIdConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new AssetIdConverter() }
    };

    [Fact]
    public void Write_SerializesAsString()
    {
        var assetId = new AssetId(5, 1, "my-asset");

        var json = JsonSerializer.Serialize(assetId, Options);

        json.Should().Be("\"5/1/my-asset\"");
    }

    [Fact]
    public void Read_DeserializesFromString()
    {
        var result = JsonSerializer.Deserialize<AssetId>("\"5/1/my-asset\"", Options);

        result.Should().NotBeNull();
        result!.Customer.Should().Be(5);
        result.Space.Should().Be(1);
        result.Asset.Should().Be("my-asset");
    }

    [Fact]
    public void RoundTrip_PreservesValue()
    {
        var original = new AssetId(2, 10, "some-image");

        var json = JsonSerializer.Serialize(original, Options);
        var result = JsonSerializer.Deserialize<AssetId>(json, Options);

        result.Should().Be(original);
    }

    [Fact]
    public void Write_UsedAsProperty_SerializesIdAsString()
    {
        var obj = new { Id = new AssetId(3, 0, "test") };

        var json = JsonSerializer.Serialize(obj, Options);

        json.Should().Contain("\"3/0/test\"");
    }
}