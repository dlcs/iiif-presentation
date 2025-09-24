using Core.Exceptions;
using DLCS;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Services.Manifests;
using Test.Helpers.Helpers;
using Test.Helpers.Settings;

namespace Services.Tests.Manifests;

public class PaintableAssetIdentifierTests
{
    private readonly DlcsSettings dlcsSettings;

    // --- Setup ---
    private PaintableAssetIdentifier pai; 
    public PaintableAssetIdentifierTests()
    {
        dlcsSettings = DefaultSettings.DlcsSettings();

        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(dlcsSettings);
        
        var logger = new NullLogger<PaintableAssetIdentifier>();
        
        pai = new(optionsMonitor,logger);
    }
    
    // --- Tests ---

    [Fact]
    public void ResolvePaintableAsset_ForUnrecgonizedIPaintable_ReturnsNull()
    {
        pai.ResolvePaintableAsset(new UnrecognizedPaintable(), -1).Should()
            .BeNull("we only support selected implementations");
    }

    [Fact]
    public void ResolvePaintableAsset_Sound_UsesAVRegex()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";
        
        var id = $"{dlcsSettings.OrchestratorUri}/iiif-av/{customerId}/{spaceId}/{assetId}/full/max/default.mp3";
        var sound = new Sound { Id = id };
        
        var result = pai.ResolvePaintableAsset(sound, customerId);
        result.Should().NotBeNull("Correct sound id was provided");
        result!.Customer.Should().Be(customerId);
        result!.Space.Should().Be(spaceId);
        result!.Asset.Should().Be(assetId);
    }
    
    [Fact]
    public void ResolvePaintableAsset_Sound_ReturnsNull_WhenUnrecognized()
    {
        var sound = new Sound { Id = "http://example.com/this/is/not/ours/item.mp3" };
        pai.ResolvePaintableAsset(sound,-1).Should().BeNull("we only support DLCS URI pattern");
    }
    
    [Fact]
    public void ResolvePaintableAsset_Video_UsesAVRegex()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";
        
        var id = $"{dlcsSettings.OrchestratorUri}/iiif-av/{customerId}/{spaceId}/{assetId}/full/full/max/max/0/default.mp4";
        var video = new Video { Id = id };
        
        var result = pai.ResolvePaintableAsset(video, customerId);
        result.Should().NotBeNull("Correct video id was provided");
        result!.Customer.Should().Be(customerId);
        result!.Space.Should().Be(spaceId);
        result!.Asset.Should().Be(assetId);
    }
    
    [Fact]
    public void ResolvePaintableAsset_Video_ReturnsNull_WhenUnrecognized()
    {
        var video = new Video { Id = "http://example.com/this/is/not/ours/item.mp4" };
        pai.ResolvePaintableAsset(video,-1).Should().BeNull("we only support DLCS URI pattern");
    }

    [Fact]
    public void ResolvePaintableAsset_Image_ResolvesFromId()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";
        
        var id = $"{dlcsSettings.OrchestratorUri}/iiif-img/{customerId}/{spaceId}/{assetId}/full/155,200/0/default.jpg";
        var image = new Image { Id = id };
        
        var result = pai.ResolvePaintableAsset(image, customerId);
        result.Should().NotBeNull("Correct image id was provided");
        result!.Customer.Should().Be(customerId);
        result!.Space.Should().Be(spaceId);
        result!.Asset.Should().Be(assetId);
    }
    
    [Fact]
    public void ResolvePaintableAsset_Image_ResolvesServiceV2()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";
        
        var id = $"{dlcsSettings.OrchestratorUri}/iiif-img/v2/{customerId}/{spaceId}/{assetId}/full/155,200/0/default.jpg";
        var image = new Image { Id = "not-valid", Service = [new ImageService2{Id = id}]};
        
        var result = pai.ResolvePaintableAsset(image, customerId);
        result.Should().NotBeNull("Correct image id in V2 image service was provided");
        result!.Customer.Should().Be(customerId);
        result!.Space.Should().Be(spaceId);
        result!.Asset.Should().Be(assetId);
    }
    
    [Fact]
    public void ResolvePaintableAsset_Image_ResolvesServiceV3()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";
        
        var id = $"{dlcsSettings.OrchestratorUri}/iiif-img/v3/{customerId}/{spaceId}/{assetId}/full/155,200/0/default.jpg";
        var image = new Image { Id = "not-valid", Service = [new ImageService3{Id = id}]};
        
        var result = pai.ResolvePaintableAsset(image, customerId);
        result.Should().NotBeNull("Correct image id in V3 image service was provided");
        result!.Customer.Should().Be(customerId);
        result!.Space.Should().Be(spaceId);
        result!.Asset.Should().Be(assetId);
    }

    [Fact]
    public void ResolvePaintableAsset_Image_ThrowsPresentationException_WhenAmbiguousIds()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";
        const string otherAssetId = "def";
        
        var id = $"{dlcsSettings.OrchestratorUri}/iiif-img/v3/{customerId}/{spaceId}/{assetId}/full/155,200/0/default.jpg";
        var otherId = $"{dlcsSettings.OrchestratorUri}/iiif-img/v3/{customerId}/{spaceId}/{otherAssetId}/full/155,200/0/default.jpg";
        var image = new Image { Id = otherId, Service = [new ImageService3{Id = id}]};
        
        Action act = () => pai.ResolvePaintableAsset(image, customerId);
        act.Should().Throw<PresentationException>("we prohibit images that point to different assets in body/services");
    }
    
    [Fact]
    public void ResolvePaintableAsset_Image_ReturnsNull_WhenBodyPointsToDifferentCustomerAsset()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";

        var id =
            $"{dlcsSettings.OrchestratorUri}/iiif-img/{customerId}/{spaceId}/{assetId}/full/155,200/0/default.jpg";
        var image = new Image { Id =  id };

        var result = pai.ResolvePaintableAsset(image, customerId + 1 /* force different id */);
        result.Should().BeNull("the resolved asset belongs to a different customer");
    }

    [Fact]
    public void ResolvePaintableAsset_Image_ReturnsNull_WhenServiceV2PointsToDifferentCustomerAsset()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";

        var id =
            $"{dlcsSettings.OrchestratorUri}/iiif-img/v2/{customerId}/{spaceId}/{assetId}/full/155,200/0/default.jpg";
        var image = new Image { Id = "not-valid", Service = [new ImageService2 { Id = id }] };

        var result = pai.ResolvePaintableAsset(image, customerId + 1 /* force different id */);
        result.Should().BeNull("the resolved asset belongs to a different customer");
    }
    
    [Fact]
    public void ResolvePaintableAsset_Image_ReturnsNull_WhenServiceV3PointsToDifferentCustomerAsset()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";

        var id =
            $"{dlcsSettings.OrchestratorUri}/iiif-img/v3/{customerId}/{spaceId}/{assetId}/full/155,200/0/default.jpg";
        var image = new Image { Id = "not-valid", Service = [new ImageService3 { Id = id }] };

        var result = pai.ResolvePaintableAsset(image, customerId + 1 /* force different id */);
        result.Should().BeNull("the resolved asset belongs to a different customer");
    }

    [Fact]
    public void ResolvePaintableAsset_Image_ReturnsNull_WhenBodyForeignDomain()
    {
        const int customerId = 1;
        const int spaceId = 2;
        const string assetId = "abc";

        var id =
            $"http://invalid.domain/iiif-img/{customerId}/{spaceId}/{assetId}/full/155,200/0/default.jpg";
        var image = new Image { Id =  id };

        var result = pai.ResolvePaintableAsset(image, customerId + 1 /* force different id */);
        result.Should().BeNull("the resolved asset is from an unrecognized domain");
    }
    
    

    // --- Helpers ---
    class UnrecognizedPaintable : IPaintable
    {
        public List<IService>? Service { get; set; }
    }
}
