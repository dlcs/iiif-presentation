using API.Converters;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Models.API.Collection;
using Models.API.Manifest;

namespace API.Tests.Converters;

public class PresentationIIIFCleanerTests
{
    [Fact]
    public void TestCleanManifest()
    {
        var manifest = new PresentationManifest
        {
            // From IIIF
            Id = "this/is/some/Id",
            Label = new LanguageMap("en", "some label"),
            Thumbnail =
            [
                new Image
                {
                    Id = "https://localhost/thumbs/12/23/blabla/full/143,200/0/default.jpg"
                }
            ],
            Rights = "https://creativecommons.org/licenses/by/4.0/",
            Items =
            [
                new Canvas
                {
                    Id = "some id"
                }
            ]
            // outside IIIF
            ,
            Slug = "some slug",
            Parent = "some parent",
            PublicId = "some public id"
        };

        var clean = PresentationIIIFCleaner.OnlyIIIFProperties(manifest);

        clean.Slug.Should().BeNull("not in IIIF");
        clean.Parent.Should().BeNull("not in IIIF");
        clean.PublicId.Should().BeNull("not in IIIF");

        clean.Rights.Should().Be(manifest.Rights, "in the IIIF");
        clean.Id.Should().Be(manifest.Id, "in the IIIF");
        clean.Thumbnail.Should().BeEquivalentTo(manifest.Thumbnail, "in the IIIF");
        clean.Items.Should().BeEquivalentTo(manifest.Items, "in the IIIF");
    }
    
    [Fact]
    public void TestCleanCollection()
    {
        var collection = new PresentationCollection
        {
            // From IIIF
            Id = "this/is/some/Id",
            Label = new LanguageMap("en", "some label"),
            Thumbnail =
            [
                new Image
                {
                    Id = "https://localhost/thumbs/12/23/blabla/full/143,200/0/default.jpg"
                }
            ],
            Rights = "https://creativecommons.org/licenses/by/4.0/"
            // outside IIIF
            ,
            Slug = "some slug",
            Parent = "some parent",
            PublicId = "some public id"
        };

        var clean = PresentationIIIFCleaner.OnlyIIIFProperties(collection);

        clean.Slug.Should().BeNull("not in IIIF");
        clean.Parent.Should().BeNull("not in IIIF");
        clean.PublicId.Should().BeNull("not in IIIF");

        clean.Rights.Should().Be(collection.Rights, "in the IIIF");
        clean.Id.Should().Be(collection.Id, "in the IIIF");
        clean.Thumbnail.Should().BeEquivalentTo(collection.Thumbnail, "in the IIIF");
    }
}
