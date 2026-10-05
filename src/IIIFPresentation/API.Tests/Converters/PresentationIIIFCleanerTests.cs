using API.Converters;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using Models.API.Collection;
using Models.API.Manifest;

namespace API.Tests.Converters;

public class PresentationIIIFCleanerTests
{
    [Fact]
    public void ToManifest_PreservesBaseIIIFProperties()
    {
        var manifest = new PresentationManifest
        {
            Label = new LanguageMap("en", ["a label"]),
            Behavior = ["auto-advance"],
            Items = [new Canvas { Id = "https://example.org/canvas/1" }],
            Context = "http://iiif.io/api/presentation/3/context.json",
            // Presentation-only properties - must not survive the clean
            Slug = "my-slug",
            PublicId = "https://example.org/1/my-slug",
            Parent = "https://example.org/1/collections/parent",
            FlatId = "internal-id"
        };

        var cleaned = manifest.ToManifest();

        cleaned.Should().BeOfType<Manifest>("the result is the plain base type, not a PresentationManifest");
        cleaned.Label.Should().BeEquivalentTo(manifest.Label);
        cleaned.Behavior.Should().BeEquivalentTo(manifest.Behavior);
        cleaned.Items.Should().HaveCount(1);
        cleaned.Items![0].Id.Should().Be("https://example.org/canvas/1");
        cleaned.Context.Should().Be(manifest.Context);
    }

    [Fact]
    public void ToCollection_PreservesBaseIIIFProperties()
    {
        var collection = new PresentationCollection
        {
            Label = new LanguageMap("en", ["a label"]),
            Behavior = ["auto-advance"],
            Items = [new Manifest { Id = "https://example.org/manifests/1" }],
            Context = "http://iiif.io/api/presentation/3/context.json",
            // Presentation-only properties - must not survive the clean
            Slug = "my-slug",
            PublicId = "https://example.org/1/my-slug",
            Parent = "https://example.org/1/collections/parent",
            FlatId = "internal-id"
        };

        var cleaned = collection.ToCollection();

        cleaned.Should().BeOfType<Collection>("the result is the plain base type, not a PresentationCollection");
        cleaned.Label.Should().BeEquivalentTo(collection.Label);
        cleaned.Behavior.Should().BeEquivalentTo(collection.Behavior);
        cleaned.Items.Should().HaveCount(1);
        cleaned.Context.Should().Be(collection.Context);
    }
}
