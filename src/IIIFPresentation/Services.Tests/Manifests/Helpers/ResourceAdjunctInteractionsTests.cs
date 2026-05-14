using Services.Manifests.Helpers;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace Services.Tests.Manifests.Helpers;

public class ResourceAdjunctInteractionsTests
{
    [Fact]
    public void GetResourceStubAssetId_ReturnsAssetIdInSpace0()
    {
        var result = ResourceAdjunctInteractions.GetResourceStubAssetId(new PresentationManifest(), 5, "my-manifest");

        result.Customer.Should().Be(5);
        result.Space.Should().Be(0);
        result.Asset.Should().Be("Manifest_my-manifest");
    }

    [Fact]
    public void GetAdjunctInteraction_ReturnsNull_WhenAdjunctsIsNull()
    {
        var result = ResourceAdjunctInteractions.GetAdjunctInteraction(new PresentationManifest { Id = "my-manifest" }, 1);

        result.Should().BeNull();
    }

    [Fact]
    public void GetAdjunctInteraction_ReturnsEmptyAdjuncts_WhenAdjunctsIsEmptyList()
    {
        var result = ResourceAdjunctInteractions.GetAdjunctInteraction(
            new PresentationManifest { Id = "my-manifest", Adjuncts = [] }, 1);

        result.Should().NotBeNull();
        result!.Adjuncts.Should().BeEmpty();
    }

    [Fact]
    public void GetAdjunctInteraction_SetsCorrectAssetId()
    {
        var result = ResourceAdjunctInteractions.GetAdjunctInteraction(
            new PresentationManifest { Id = "my-manifest", Adjuncts = [] }, 5);

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

        ResourceAdjunctInteractions.GetAdjunctInteraction(
            new PresentationManifest { Id = "res-1", Adjuncts = adjuncts }, 3);

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

        var result = ResourceAdjunctInteractions.GetAdjunctInteraction(
            new PresentationManifest { Id = "res-1", Adjuncts = adjuncts }, 1);

        result!.Adjuncts.Should().HaveCount(2);
    }
}