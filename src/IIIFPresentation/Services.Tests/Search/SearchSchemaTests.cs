using Services.Search;

namespace Services.Tests.Search;

public class SearchSchemaTests
{
    [Fact]
    public void SearchSchema_UsesStableCustomerScopedNames()
    {
        var settings = new TypesenseSettings
        {
            CollectionPrefix = "iiif_presentation"
        };

        SearchSchema.GetAliasName(settings, 42).Should().Be("iiif_presentation_customer_42");
        SearchSchema.GetStateCollectionName(settings).Should().Be("iiif_presentation__state");
        SearchSchema.GetStateId(42).Should().Be("customer:42");
        SearchSchema.GenerateCollectionName(settings, 42).Should().StartWith("iiif_presentation_customer_42_v2_");
    }
}
