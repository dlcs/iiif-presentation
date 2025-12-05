using System.Net;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.Extensions.DependencyInjection;
using Models.API.General;
using Models.API.Manifest;
using Newtonsoft.Json.Linq;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ModifyManifestMixedCanvasLabelTests : IClassFixture<PresentationAppFactory<Program>>,
    IClassFixture<StorageFixture>
{
    private readonly HttpClient httpClient;
    private const int Customer = 1;
    private const int NewlyCreatedSpace = 999;
    private static readonly IDlcsApiClient DLCSApiClient = A.Fake<IDlcsApiClient>();
    private static readonly IDlcsOrchestratorClient DLCSOrchestratorClient = A.Fake<IDlcsOrchestratorClient>();

    public ModifyManifestMixedCanvasLabelTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        // Always return Space 999 when call to create space
        A.CallTo(() => DLCSApiClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });
        
        // Echo back "batch" value set in first Asset
        A.CallTo(() => DLCSApiClient.IngestAssets(Customer, A<List<JObject>>._, A<CancellationToken>._))
            .ReturnsLazily(x => Task.FromResult(
                new List<Batch>
                {
                    new()
                    {
                        ResourceId = x.Arguments.Get<List<JObject>>("images").First().GetValue("batch").ToString(),
                        Submitted = DateTime.Now
                    }
                }));
        
        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer, 
                A<ICollection<string>>._, A<CancellationToken>._))
            .ReturnsLazily(x =>
                Task.FromResult(new List<JObject>() as IList<JObject>));
        
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services
                .AddSingleton(DLCSApiClient)
                .AddSingleton(DLCSOrchestratorClient)
        );

        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenMatchingCanvasLabel()
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasLabel = label
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                }
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(1);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        paintedResource.CanvasPainting.Label.Should().BeNull();
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenMatchingLabel()
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        Label = label
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(1);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be(label.First().Key);
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenNonMatchingLabel()
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        Label = new LanguageMap("anotherLabel", "label to not match"),
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(1);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenMatchingCanvasLabelAndNonMatchingLabel()
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        Label = new LanguageMap("anotherLabel", "label to not match"),
                        CanvasLabel = label
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(1);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Fact]
    public async Task Create_SuccessfullyCreatesSingleItemManifest_WhenMatchingCanvasLabelAndLabel()
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        Label = label,
                        CanvasLabel = label
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(1);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be(label.First().Key);
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Fact]
    public async Task Create_ThrowsError_WhenNonMatchingCanvasLabelAndMatchingLabel()
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        Label = label,
                        CanvasLabel = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        error.Detail.Should().Be($"Canvas painting with id {canvasId} does not have a matching canvas label");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemCanvasManifest_WhenMatchingLabel(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(2);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        var secondPaintedResource = ingestedManifest.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemCanvasManifest_WhenMatchingCanvasLabel(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match"),
                        CanvasLabel = label
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(2);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        var secondPaintedResource = ingestedManifest.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemCanvasManifest_WhenMatchingCanvasLabelFromNotTheFirstCanvas(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match"),
                        CanvasLabel = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(2);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.Should().BeNull();
        var secondPaintedResource = ingestedManifest.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
        secondPaintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
    }
    
    [Theory]
    [InlineData(0, 1, 0, 0, "_1")]
    [InlineData(1, 0, 0, 0, "_2")]
    [InlineData(1, null, 0, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemCanvasManifest_WhenMatchingCanvasLabelOrderedBadly(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match"),
                        CanvasLabel = new LanguageMap("mismatch", "canvas label to not match")
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match"),
                        CanvasLabel = label
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(2);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        var secondPaintedResource = ingestedManifest.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_FailsToCreateMultiItemCanvasManifest_WhenNonMatchingCanvasLabelFromNotTheFirstCanvas(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match"),
                        CanvasLabel = new LanguageMap("none matching canvas", "label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        error.Detail.Should().Be($"Canvas painting with id {canvasId} does not have a matching canvas label");
    }
    
    [Theory]
    [InlineData(0, 0, 0, 1, "_1")]
    [InlineData(0, 0, 1, 0, "_2")]
    [InlineData(0, null, 1, null, "_3")]
    public async Task Create_SuccessfullyCreatesMultiItemCanvasManifest_WhenNoCanvasLabelSet(int firstCanvasOrder, 
        int? firstChoiceOrder, int secondCanvasOrder, int? secondChoiceOrder, string slugAppend)
    {
        // Arrange
        var (slug, _, _, canvasId) = TestIdentifiers.SlugResourceAssetCanvas();
        slug += slugAppend;
        
        var label = new LanguageMap("label", "label to match");
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = canvasId,
                    Label = label
                }
            ],
            PaintedResources = [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = firstCanvasOrder,
                        ChoiceOrder = firstChoiceOrder,
                        Label = new LanguageMap("anotherLabel", "label to not match")
                    },
                    Asset = new(new JProperty("id", "first"), new JProperty("batch", TestIdentifiers.BatchId()))
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasId,
                        CanvasOrder = secondCanvasOrder,
                        ChoiceOrder = secondChoiceOrder,
                        Label = new LanguageMap("anotherLabel2", "second label to not match")
                    },
                    Asset = JObject.Parse(@"{""id"": ""second""}")
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var ingestedManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        ingestedManifest.PaintedResources.Should().HaveCount(2);
        ingestedManifest.Items.First().Id.Should().Be($"https://localhost:7230/1/canvases/{canvasId}");
        var paintedResource = ingestedManifest.PaintedResources.First();
        paintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        paintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel");
        paintedResource.CanvasPainting.CanvasLabel.First().Key.Should().Be(label.First().Key);
        var secondPaintedResource = ingestedManifest.PaintedResources.Last();
        secondPaintedResource.CanvasPainting.CanvasId.Should().Be($"http://localhost/{Customer}/canvases/{canvasId}");
        secondPaintedResource.CanvasPainting.Label.First().Key.Should().Be("anotherLabel2");
        secondPaintedResource.CanvasPainting.CanvasLabel.Should().BeNull();
    }
}
