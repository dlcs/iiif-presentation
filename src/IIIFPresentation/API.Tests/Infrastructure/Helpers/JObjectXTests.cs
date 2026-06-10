using API.Infrastructure.Helpers;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Services.Manifests.Model;

namespace API.Tests.Infrastructure.Helpers;

public class JObjectXTests
{
    private static readonly AssetId AssetId = new(1, 10, "test-asset");

    [Fact]
    public void SetExistingAdjunctIds_DoesNothing_WhenAdjunctInteractionsIsNull()
    {
        var jObject = JObject.Parse("{}");

        Action action = () => jObject.SetExistingAdjunctIds(null, AssetId);

        action.Should().NotThrow();
    }

    [Fact]
    public void SetExistingAdjunctIds_DoesNothing_WhenNoMatchingAssetId()
    {
        var jObject = JObject.Parse(@"{ ""adjuncts"": [{ ""id"": ""foo"" }] }");
        var otherAssetId = new AssetId(1, 10, "other-asset");
        var interaction = new AdjunctInteraction { AssetId = otherAssetId, Adjuncts = [] };

        jObject.SetExistingAdjunctIds([interaction], AssetId);

        interaction.ExistingAdjunctIds.Should().BeNull();
    }

    [Fact]
    public void SetExistingAdjunctIds_SetsEmptyList_WhenJObjectHasNoAdjuncts()
    {
        var jObject = JObject.Parse("{}");
        var interaction = new AdjunctInteraction { AssetId = AssetId, Adjuncts = [] };

        jObject.SetExistingAdjunctIds([interaction], AssetId);

        interaction.ExistingAdjunctIds.Should().BeEmpty();
    }

    [Fact]
    public void SetExistingAdjunctIds_SetsIds_FromAdjunctsArray()
    {
        var jObject = JObject.Parse(@"{ ""adjuncts"": [{ ""id"": ""foo"" }, { ""id"": ""bar"" }] }");
        var interaction = new AdjunctInteraction { AssetId = AssetId, Adjuncts = [] };

        jObject.SetExistingAdjunctIds([interaction], AssetId);

        interaction.ExistingAdjunctIds.Should().BeEquivalentTo(["foo", "bar"]);
    }

    [Fact]
    public void SetExistingAdjunctIds_FiltersOut_AdjunctsWithMissingId()
    {
        var jObject = JObject.Parse(@"{ ""adjuncts"": [{ ""id"": ""foo"" }, { ""type"": ""no-id"" }] }");
        var interaction = new AdjunctInteraction { AssetId = AssetId, Adjuncts = [] };

        jObject.SetExistingAdjunctIds([interaction], AssetId);

        interaction.ExistingAdjunctIds.Should().BeEquivalentTo("foo");
    }
}
