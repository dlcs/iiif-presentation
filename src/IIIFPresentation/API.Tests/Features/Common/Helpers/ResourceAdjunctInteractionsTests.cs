using API.Features.Common.Helpers;
using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Tests.Features.Common.Helpers;

public class ResourceAdjunctInteractionsTests
{
    [Fact]
    public void GetResourceStubAssetId_ReturnsAssetIdInSpace0()
    {
        var result = ResourceAdjunctInteractions.GetResourceStubAssetId(5, "my-manifest");

        result.Customer.Should().Be(5);
        result.Space.Should().Be(0);
        result.Asset.Should().Be("Manifest_my-manifest");
    }

    [Fact]
    public void GetAdjunctInteraction_ReturnsNull_WhenAdjunctsIsNull()
    {
        var result = ResourceAdjunctInteractions.GetAdjunctInteraction(null, 1, "my-manifest");

        result.Should().BeNull();
    }

    [Fact]
    public void GetAdjunctInteraction_ReturnsEmptyAdjuncts_WhenAdjunctsIsEmptyList()
    {
        var result = ResourceAdjunctInteractions.GetAdjunctInteraction([], 1, "my-manifest");

        result.Should().NotBeNull();
        result!.Adjuncts.Should().BeEmpty();
    }

    [Fact]
    public void GetAdjunctInteraction_SetsCorrectAssetId()
    {
        var result = ResourceAdjunctInteractions.GetAdjunctInteraction([], 5, "my-manifest");

        result!.AssetId.Should().Be(new AssetId(5, 0, "Manifest_my-manifest"));
    }

    [Fact]
    public void GetAdjunctInteraction_StampsAssetPropertyOnEachAdjunct()
    {
        var adjuncts = new List<JObject>
        {
            JObject.Parse("""{ "id": "mets.xml", "mediaType": "text/xml" }"""),
            JObject.Parse("""{ "id": "thumb.jpg", "mediaType": "image/jpeg" }""")
        };

        ResourceAdjunctInteractions.GetAdjunctInteraction(adjuncts, 3, "res-1");

        adjuncts[0]["asset"]!.Value<string>().Should().Be("3/0/Manifest_res-1");
        adjuncts[1]["asset"]!.Value<string>().Should().Be("3/0/Manifest_res-1");
    }

    [Fact]
    public void GetAdjunctInteraction_ReturnedAdjunctsContainAllItems()
    {
        var adjuncts = new List<JObject>
        {
            JObject.Parse("""{ "id": "mets.xml" }"""),
            JObject.Parse("""{ "id": "thumb.jpg" }""")
        };

        var result = ResourceAdjunctInteractions.GetAdjunctInteraction(adjuncts, 1, "res-1");

        result!.Adjuncts.Should().HaveCount(2);
    }
}