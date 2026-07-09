using IIIF.Serialisation;
using Models.API.Collection;
using Models.API.Manifest;

namespace Services.Tests;

public class IIIFSerialisationXTests
{
    [Fact]
    public void ToManifest_StripsAllPresentationPropertyKeys()
    {
        var json = """
                   {
                     "@context": "http://iiif.io/api/presentation/3/context.json",
                     "id": "https://example.org/manifest/1",
                     "type": "Manifest",
                     "slug": "test-slug",
                     "publicId": "pub-1",
                     "parent": "parent-id",
                     "created": "2024-01-01T00:00:00Z",
                     "modified": "2024-01-01T00:00:00Z",
                     "createdBy": "user",
                     "modifiedBy": "user",
                     "flatId": "flat-1",
                     "paintedResources": [],
                     "space": "space-1",
                     "ingesting": null,
                     "adjuncts": []
                   }
                   """;

        var result = json.ToManifest();

        result.Should().NotBeNull();
        result!.AdditionalProperties.Should().NotContainKey("slug");
        result.AdditionalProperties.Should().NotContainKey("publicId");
        result.AdditionalProperties.Should().NotContainKey("parent");
        result.AdditionalProperties.Should().NotContainKey("created");
        result.AdditionalProperties.Should().NotContainKey("modified");
        result.AdditionalProperties.Should().NotContainKey("createdBy");
        result.AdditionalProperties.Should().NotContainKey("modifiedBy");
        result.AdditionalProperties.Should().NotContainKey("flatId");
        result.AdditionalProperties.Should().NotContainKey("paintedResources");
        result.AdditionalProperties.Should().NotContainKey("space");
        result.AdditionalProperties.Should().NotContainKey("ingesting");
        result.AdditionalProperties.Should().NotContainKey("adjuncts");
    }

    [Fact]
    public void ToManifest_PreservesUnknownCustomProperties()
    {
        var json = """
                   {
                     "@context": "http://iiif.io/api/presentation/3/context.json",
                     "id": "https://example.org/manifest/1",
                     "type": "Manifest",
                     "slug": "test-slug",
                     "myCustomProp": "keep-me"
                   }
                   """;

        var result = json.ToManifest();

        result.Should().NotBeNull();
        result!.AdditionalProperties.Should().NotContainKey("slug");
        result.AdditionalProperties.Should().ContainKey("myCustomProp");
    }

    [Fact]
    public void ToManifest_PreservesStandardManifestProperties()
    {
        var json = """
                   {
                     "@context": "http://iiif.io/api/presentation/3/context.json",
                     "id": "https://example.org/manifest/1",
                     "type": "Manifest",
                     "label": {"en": ["Test Manifest"]},
                     "slug": "test-slug"
                   }
                   """;

        var result = json.ToManifest();

        result.Should().NotBeNull();
        result!.Id.Should().Be("https://example.org/manifest/1");
        result.Label.Should().NotBeNull();
    }

    [Fact]
    public void ToCollection_StripsAllPresentationPropertyKeys()
    {
        var json = """
                   {
                     "@context": "http://iiif.io/api/presentation/3/context.json",
                     "id": "https://example.org/collection/1",
                     "type": "Collection",
                     "slug": "test-slug",
                     "publicId": "pub-1",
                     "flatId": "flat-1",
                     "parent": "parent-id",
                     "itemsOrder": 1,
                     "totalItems": 5,
                     "created": "2024-01-01T00:00:00Z",
                     "modified": "2024-01-01T00:00:00Z",
                     "createdBy": "user",
                     "modifiedBy": "user",
                     "tags": "tag1,tag2",
                     "totals": null,
                     "view": null
                   }
                   """;

        var result = json.ToCollection();

        result.Should().NotBeNull();
        result!.AdditionalProperties.Should().NotContainKey("slug");
        result.AdditionalProperties.Should().NotContainKey("publicId");
        result.AdditionalProperties.Should().NotContainKey("flatId");
        result.AdditionalProperties.Should().NotContainKey("parent");
        result.AdditionalProperties.Should().NotContainKey("itemsOrder");
        result.AdditionalProperties.Should().NotContainKey("totalItems");
        result.AdditionalProperties.Should().NotContainKey("created");
        result.AdditionalProperties.Should().NotContainKey("modified");
        result.AdditionalProperties.Should().NotContainKey("createdBy");
        result.AdditionalProperties.Should().NotContainKey("modifiedBy");
        result.AdditionalProperties.Should().NotContainKey("tags");
        result.AdditionalProperties.Should().NotContainKey("totals");
        result.AdditionalProperties.Should().NotContainKey("view");
    }

    [Fact]
    public void ToCollection_PreservesUnknownCustomProperties()
    {
        var json = """
                   {
                     "@context": "http://iiif.io/api/presentation/3/context.json",
                     "id": "https://example.org/collection/1",
                     "type": "Collection",
                     "slug": "test-slug",
                     "myCustomProp": "keep-me"
                   }
                   """;

        var result = json.ToCollection();

        result.Should().NotBeNull();
        result!.AdditionalProperties.Should().NotContainKey("slug");
        result.AdditionalProperties.Should().ContainKey("myCustomProp");
    }

    [Fact]
    public void ToManifest_StrippedPropertiesAbsentFromSerialisation()
    {
        var json = """
                   {
                     "@context": "http://iiif.io/api/presentation/3/context.json",
                     "id": "https://example.org/manifest/1",
                     "type": "Manifest",
                     "slug": "test-slug",
                     "parent": "parent-id"
                   }
                   """;

        var result = json.ToManifest();
        var serialised = result!.AsJson();

        serialised.Should().NotContain("\"slug\"");
        serialised.Should().NotContain("\"parent\"");
        serialised.Should().Contain("\"id\"");
    }

    [Fact]
    public void ToCollection_StrippedPropertiesAbsentFromSerialisation()
    {
        var json = """
                   {
                     "@context": "http://iiif.io/api/presentation/3/context.json",
                     "id": "https://example.org/collection/1",
                     "type": "Collection",
                     "slug": "test-slug",
                     "parent": "parent-id"
                   }
                   """;

        var result = json.ToCollection();
        var serialised = result!.AsJson();

        serialised.Should().NotContain("\"slug\"");
        serialised.Should().NotContain("\"parent\"");
        serialised.Should().Contain("\"id\"");
    }

    [Fact]
    public void ToManifest_PresentationPropertyKeys_CoversAllPresentationManifestProperties()
        => PresentationManifest.PresentationPropertyKeys.Should().HaveCount(16);

    [Fact]
    public void ToCollection_PresentationPropertyKeys_CoversAllPresentationCollectionProperties()
        => PresentationCollection.PresentationPropertyKeys.Should().HaveCount(13);
}
