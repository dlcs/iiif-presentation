using API.Features.Storage.Helpers;
using IIIF.Presentation.V3.Content;
using Models.API.Collection;

namespace API.Tests.Features.Storage.Helpers;

public class PresentationCollectionXTests
{
    [Fact]
    public void GetThumbnail_Null_IfNoThumbs()
        => new PresentationCollection().GetThumbnail().Should().BeNull("No thumbnails");

    [Fact]
    public void GetThumbnail_Null_IfNoImageThumbs()
    {
        var presentationCollection = new PresentationCollection
        {
            Thumbnail = [new ExternalResource("ForTestOnly")]
        };

        presentationCollection.GetThumbnail().Should().BeNull("No Image thumbnails");
    }

    [Fact]
    public void GetThumbnail_ReturnsThumbnail()
    {
        var presentationCollection = new PresentationCollection
        {
            Thumbnail =
            [
                new Image { Id = "some/image1.jpg" }
            ]
        };
        
        presentationCollection.GetThumbnail().Should().Be("some/image1.jpg");
    }
}
