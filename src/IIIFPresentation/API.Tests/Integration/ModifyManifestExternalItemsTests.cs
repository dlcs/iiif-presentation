using System.Net;
using API.Infrastructure.Helpers;
using API.Tests.Integration.Infrastructure;
using Core.Helpers;
using Core.Response;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

/// <summary>
/// Tests for creating/updating manifests with external "items"
/// </summary>
/// <remarks>
/// <see cref="ModifyManifestCreateTests"/> and <see cref="ModifyManifestUpdateTests"/> for basic tests
/// </remarks>
[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestExternalItemsTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private const int Customer = 1;

    public ModifyManifestExternalItemsTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));
        
        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task CreateManifest_ExternalItems_ReturnsManifest()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://iiif.example/{slug}.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest.Id.Should().NotBeNull();
        responseManifest.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        responseManifest.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        responseManifest.CreatedBy.Should().Be("Admin");
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
        responseManifest.PaintedResources.Should()
            .ContainSingle(pr => pr.CanvasPainting.CanvasOriginalId == $"https://iiif.example/{slug}.json");
    }
    
    [Fact]
    public async Task CreateManifest_ExternalItems_CreatedDBRecord()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://iiif.example/{slug}.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPainting = fromDatabase.CanvasPaintings.Single();

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();
        canvasPainting.Id.Should().NotBeNullOrEmpty();
        canvasPainting.CanvasOriginalId.Should().Be("https://iiif.example/CreateManifest_ExternalItems_CreatedDBRecord.json");
        canvasPainting.CanvasOrder.Should().Be(0);
        canvasPainting.ChoiceOrder.Should().BeNull();
        canvasPainting.StaticWidth.Should().Be(1200);
        canvasPainting.StaticHeight.Should().Be(1800);
    }
    
    [Fact]
    public async Task CreateManifest_ExternalItems_MultipleCanvases_CreatedDBRecord()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://iiif.example/{slug}.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }},
            ]
        }},
        {{
            ""id"": ""https://iiif.example/{slug}2.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/2"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0002-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1000,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p2""
                        }}
                    ]
                }},
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();

        var canvasPainting1 = canvasPaintings.First();
        canvasPainting1.Id.Should().NotBeNullOrEmpty();
        canvasPainting1.CanvasOriginalId.Should().Be("https://iiif.example/CreateManifest_ExternalItems_MultipleCanvases_CreatedDBRecord.json");
        canvasPainting1.CanvasOrder.Should().Be(0);
        canvasPainting1.ChoiceOrder.Should().BeNull();
        canvasPainting1.StaticWidth.Should().Be(1200);
        canvasPainting1.StaticHeight.Should().Be(1800);
        
        var canvasPainting2 = canvasPaintings.Last();
        canvasPainting2.Id.Should().NotBeNullOrEmpty();
        canvasPainting2.CanvasOriginalId.Should().Be("https://iiif.example/CreateManifest_ExternalItems_MultipleCanvases_CreatedDBRecord2.json");
        canvasPainting2.CanvasOrder.Should().Be(1);
        canvasPainting2.ChoiceOrder.Should().BeNull();
        canvasPainting2.StaticWidth.Should().Be(1200);
        canvasPainting2.StaticHeight.Should().Be(1000);
    }
    
    [Fact]
    public async Task CreateManifest_ExternalItemsWithChoices_CreatedDBRecord()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://iiif.example/{slug}.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""type"": ""Choice"",
                                ""items"": [
                                  {{
                                    ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                    ""type"": ""Image"",
                                    ""width"": 617,
                                    ""height"": 1024,
                                    ""format"": ""image/jpeg"",
                                    ""service"": [
                                      {{
                                        ""@id"": ""https://iiif.org/image/002.jp2"",
                                        ""@type"": ""ImageService2"",
                                        ""profile"": ""http://iiif.io/api/image/2/level1.json""
                                      }}
                                    ]
                                  }},
                                  {{
                                    ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                    ""type"": ""Image"",
                                    ""width"": 617,
                                    ""height"": 1024,
                                    ""format"": ""image/jpeg"",
                                    ""service"": [
                                      {{
                                        ""@id"": ""https://iiif.org/image/001.jp2"",
                                        ""@type"": ""ImageService2"",
                                        ""profile"": ""http://iiif.io/api/image/2/level1.json""
                                      }}
                                    ]
                                  }}
                                ]
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();
        
        var canvasPainting1 = canvasPaintings.First();
        canvasPainting1.Id.Should().NotBeNullOrEmpty();
        canvasPainting1.CanvasOriginalId.Should().Be("https://iiif.example/CreateManifest_ExternalItemsWithChoices_CreatedDBRecord.json");
        canvasPainting1.CanvasOrder.Should().Be(0);
        canvasPainting1.ChoiceOrder.Should().Be(1);
        canvasPainting1.StaticWidth.Should().Be(617);
        canvasPainting1.StaticHeight.Should().Be(1024);
        
        var canvasPainting2 = canvasPaintings.Last();
        canvasPainting2.Id.Should().Be(canvasPainting1.Id, "Canvas choices share same id");
        canvasPainting2.CanvasOriginalId.Should().Be("https://iiif.example/CreateManifest_ExternalItemsWithChoices_CreatedDBRecord.json");
        canvasPainting2.CanvasOrder.Should().Be(0, "Canvas choices share same order");
        canvasPainting2.ChoiceOrder.Should().Be(2);
        canvasPainting2.StaticWidth.Should().Be(617);
        canvasPainting2.StaticHeight.Should().Be(1024);
    }
    
    [Fact]
    public async Task CreateManifest_ExternalItems_MultipleImageComposition_CreatedDBRecord()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://iiif.example/{slug}.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.example/{slug}.json""
                        }},
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0002-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1000,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.example/{slug}.json#xywh=300,400,500,600""
                        }}
                    ]
                }},
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();

        var canvasPainting1 = canvasPaintings.First();
        canvasPainting1.Id.Should().NotBeNullOrEmpty();
        canvasPainting1.CanvasOriginalId.Should().Be($"https://iiif.example/{slug}.json");
        canvasPainting1.CanvasOrder.Should().Be(0);
        canvasPainting1.ChoiceOrder.Should().BeNull();
        canvasPainting1.StaticWidth.Should().Be(1200);
        canvasPainting1.StaticHeight.Should().Be(1800);
        
        var canvasPainting2 = canvasPaintings.Last();
        canvasPainting2.Id.Should().NotBeNullOrEmpty();
        canvasPainting2.CanvasOriginalId.Should().Be($"https://iiif.example/{slug}.json");
        canvasPainting2.CanvasOrder.Should().Be(1);
        canvasPainting2.ChoiceOrder.Should().BeNull();
        canvasPainting2.StaticWidth.Should().Be(1200);
        canvasPainting2.StaticHeight.Should().Be(1000);
        canvasPainting2.Target.Should().Be($"https://iiif.example/{slug}.json#xywh=300,400,500,600");
        
        canvasPainting2.Id.Should().Be(canvasPainting1.Id, "Items are on same canvas so have same id");
    }
    
    [Fact]
    public async Task CreateManifest_ExternalItems_MultipleImageCompositionAndChoice_CreatedDBRecord()
    {
        // Verify that we can successfully handle a payload with 2 canvases
        // - the first has 3 painting annos; first 2 are composite, the 3rd a choice of 2 (so 4 overall CanvasPainting)
        // - the second has a single painting annotation
        var slug = TestIdentifiers.Id();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://iiif.example/{slug}.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.example/{slug}.json""
                        }},
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0002-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1000,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.example/{slug}.json#xywh=300,400,500,600""
                        }},
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0033-choice/annotation/p0003-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""type"": ""Choice"",
                                ""items"": [
                                    {{
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg"",
                                        ""type"": ""Image"",
                                        ""format"": ""image/jpeg"",
                                        ""width"": 2000,
                                        ""height"": 1271,
                                        ""label"": {{
                                            ""en"": [
                                                ""Natural Light""
                                            ]
                                        }},
                                        ""service"": [
                                            {{
                                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural"",
                                                ""type"": ""ImageService3"",
                                                ""profile"": ""level1""
                                            }}
                                        ]
                                    }},
                                    {{
                                        ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/max/0/default.jpg"",
                                        ""type"": ""Image"",
                                        ""format"": ""image/jpeg"",
                                        ""width"": 2001,
                                        ""height"": 1272,
                                        ""label"": {{
                                            ""en"": [
                                                ""X-Ray""
                                            ]
                                        }},
                                        ""service"": [
                                            {{
                                                ""id"": ""https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray"",
                                                ""type"": ""ImageService3"",
                                                ""profile"": ""level1""
                                            }}
                                        ]
                                    }}
                                ]
                            }},
                            ""target"": ""https://iiif.example/{slug}.json#xywh=0,0,1091,1232""
                        }}
                    ]
                }},
            ]
        }},
        {{
            ""id"": ""https://iiif.example/{slug}_1.json"",
            ""type"": ""Canvas"",
            ""height"": 1200,
            ""width"": 1800,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.example/{slug}_1.json""
                        }}
                    ]
                }},
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();

        var canvasPainting1 = canvasPaintings[0];
        canvasPainting1.Id.Should().NotBeNullOrEmpty();
        canvasPainting1.CanvasOriginalId.Should().Be($"https://iiif.example/{slug}.json");
        canvasPainting1.CanvasOrder.Should().Be(0);
        canvasPainting1.ChoiceOrder.Should().BeNull();
        canvasPainting1.StaticWidth.Should().Be(1200);
        canvasPainting1.StaticHeight.Should().Be(1800);
        
        var canvasPainting2 = canvasPaintings[1];
        canvasPainting2.Id.Should().NotBeNullOrEmpty();
        canvasPainting2.CanvasOriginalId.Should().Be($"https://iiif.example/{slug}.json");
        canvasPainting2.CanvasOrder.Should().Be(1);
        canvasPainting2.ChoiceOrder.Should().BeNull();
        canvasPainting2.StaticWidth.Should().Be(1200);
        canvasPainting2.StaticHeight.Should().Be(1000);
        canvasPainting2.Target.Should().Be($"https://iiif.example/{slug}.json#xywh=300,400,500,600");

        var canvasPainting3 = canvasPaintings[2];
        canvasPainting3.Id.Should().NotBeNullOrEmpty();
        canvasPainting3.CanvasOriginalId.Should().Be($"https://iiif.example/{slug}.json");
        canvasPainting3.CanvasOrder.Should().Be(2);
        canvasPainting3.ChoiceOrder.Should().Be(1);
        canvasPainting3.StaticWidth.Should().Be(2000);
        canvasPainting3.StaticHeight.Should().Be(1271);
        canvasPainting3.Target.Should().Be($"https://iiif.example/{slug}.json#xywh=0,0,1091,1232");
        
        var canvasPainting4 = canvasPaintings[3];
        canvasPainting4.Id.Should().NotBeNullOrEmpty();
        canvasPainting4.CanvasOriginalId.Should().Be($"https://iiif.example/{slug}.json");
        canvasPainting4.CanvasOrder.Should().Be(2);
        canvasPainting4.ChoiceOrder.Should().Be(2);
        canvasPainting4.StaticWidth.Should().Be(2001);
        canvasPainting4.StaticHeight.Should().Be(1272);
        canvasPainting4.Target.Should().Be(null);
        
        var canvasPainting5 = canvasPaintings[4];
        canvasPainting5.Id.Should().NotBeNullOrEmpty();
        canvasPainting5.CanvasOriginalId.Should().Be($"https://iiif.example/{slug}_1.json");
        canvasPainting5.CanvasOrder.Should().Be(3);
        canvasPainting5.ChoiceOrder.Should().BeNull();
        canvasPainting5.StaticWidth.Should().Be(1200);
        canvasPainting5.StaticHeight.Should().Be(1800);
        canvasPainting5.Target.Should().Be(null);

        var sharedCanvasId = canvasPaintings.Where(c => c.CanvasOrder != 3).Select(cp => cp.Id).Distinct();
        sharedCanvasId.Should().HaveCount(1, "All items are on same canvas so have same id");
        canvasPainting5.Id.Should()
            .NotBe(sharedCanvasId.First(), "CP5 is on a different canvas, therefor different id");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_ExternalItems_ReturnsManifest()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://iiif.example/{slug}.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest.Id.Should().EndWith(id);
        responseManifest.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        responseManifest.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        responseManifest.CreatedBy.Should().Be("Admin");
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be("http://localhost/1/collections/root");
        responseManifest.PaintedResources.Should()
            .ContainSingle(pr => pr.CanvasPainting.CanvasOriginalId == $"https://iiif.example/{slug}.json");
        responseManifest.PublicId.Should().Be($"https://localhost:7230/1/{slug}", "different host due to using the settings based path generator");
        responseManifest.FlatId.Should().Be(id);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_ExternalItems_CreatedDBRecord()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://iiif.example/{slug}.json"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": 1800,
                                ""width"": 1200
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPainting = fromDatabase.CanvasPaintings.Single();

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();
        canvasPainting.Id.Should().NotBeNullOrEmpty();
        canvasPainting.CanvasOriginalId.Should().Be($"https://iiif.example/{slug}.json");
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItems_NoChanges_ReturnsManifest()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var dbCanvasPainting =
            (await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, createdDate: DateTime.UtcNow.AddDays(-1),
                height: 1800, width: 1200,
                canvasOriginalId: new Uri("https://iiif.io/api/eclipse"))).Entity;
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{dbCanvasPainting.CanvasOriginalId}"",
            ""type"": ""Canvas"",
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": {dbCanvasPainting.StaticHeight},
                                ""width"": {dbCanvasPainting.StaticWidth},
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}", manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest.Id.Should().EndWith(dbManifest.Id);
        responseManifest.Created.Should().BeCloseTo(dbManifest.Created, TimeSpan.FromSeconds(2));
        responseManifest.Modified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        responseManifest.CreatedBy.Should().Be("Admin");
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be("http://localhost/1/collections/root");
        responseManifest.PaintedResources.Should()
            .ContainSingle(pr => pr.CanvasPainting.CanvasOriginalId == dbCanvasPainting.CanvasOriginalId.ToString());
        responseManifest.PublicId.Should().Be($"https://localhost:7230/1/{slug}", "different host due to using the settings based path generator");
        responseManifest.FlatId.Should().Be(dbManifest.Id);
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItems_NoChanges_UpdatesDBRecord()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var dbCanvasPainting =
            (await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, createdDate: DateTime.UtcNow.AddDays(-1),
                height: 1800, width: 1200,
                canvasOriginalId: new Uri("https://iiif.io/api/eclipse"))).Entity;
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{dbCanvasPainting.CanvasOriginalId}"",
            ""type"": ""Canvas"",
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": {dbCanvasPainting.StaticHeight},
                                ""width"": {dbCanvasPainting.StaticWidth},
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}", manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == dbManifest.Id);
        var canvasPainting = fromDatabase.CanvasPaintings.Single();

        fromDatabase.Should().NotBeNull();
        canvasPainting.CanvasPaintingId.Should().Be(dbCanvasPainting.CanvasPaintingId);
        canvasPainting.Modified.Should().BeAfter(dbCanvasPainting.Modified, "Item modified");
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItems_UpdateCanvas_UpdatesDBRecord()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var dbCanvasPainting =
            (await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, createdDate: DateTime.UtcNow.AddDays(-1),
                height: 1800, width: 1200,
                canvasOriginalId: new Uri("https://iiif.io/api/eclipse"))).Entity;
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{dbCanvasPainting.CanvasOriginalId}"",
            ""type"": ""Canvas"",
            ""label"": {{ ""en"": [ ""The show must go on"" ] }},
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                                ""height"": {dbCanvasPainting.StaticHeight},
                                ""width"": {dbCanvasPainting.StaticWidth},
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}", manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == dbManifest.Id);
        var canvasPainting = fromDatabase.CanvasPaintings.Single();

        fromDatabase.Should().NotBeNull();
        canvasPainting.CanvasOrder.Should().Be(0);
        canvasPainting.CanvasPaintingId.Should().Be(dbCanvasPainting.CanvasPaintingId);
        canvasPainting.Modified.Should().BeAfter(dbCanvasPainting.Modified, "Item modified");
        canvasPainting.Label.Should().BeEquivalentTo(new LanguageMap("en", "The show must go on"));
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItems_AddNewCanvasReordered_UpdatesDBRecord()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var originalId = new Uri("https://iiif.io/api/first");
        var newId = new Uri("https://iiif.io/api/newly-added");
        var dbCanvasPainting =
            (await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, createdDate: DateTime.UtcNow.AddDays(-1),
                canvasOriginalId: originalId)).Entity;
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{newId}"",
            ""type"": ""Canvas"",
            ""label"": {{ ""en"": [ ""This was inserted ahead of original"" ] }},
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/2/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                            }},
                            ""target"": ""{newId}""
                        }}
                    ]
                }}
            ]
        }},
        {{
            ""id"": ""{dbCanvasPainting.CanvasOriginalId}"",
            ""type"": ""Canvas"",
            ""label"": {{ ""en"": [ ""This will be updated with new order"" ] }},
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                            }},
                            ""target"": ""{dbCanvasPainting.CanvasOriginalId}""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}", manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == dbManifest.Id);
        fromDatabase.Should().NotBeNull();

        fromDatabase.CanvasPaintings.Should().HaveCount(2);
        
        var initial = fromDatabase.CanvasPaintings.Single(cp => cp.CanvasOriginalId == originalId);
        initial.CanvasPaintingId.Should().Be(dbCanvasPainting.CanvasPaintingId);
        initial.CanvasOrder.Should().Be(1, "Initial painting anno is now second canvas");
        initial.Modified.Should().BeAfter(dbCanvasPainting.Modified, "Item modified");
        initial.Label.Should().BeEquivalentTo(new LanguageMap("en", "This will be updated with new order"));
        
        var newCanvas = fromDatabase.CanvasPaintings.Single(cp => cp.CanvasOriginalId == newId);
        newCanvas.CanvasOrder.Should().Be(0, "New painting anno inserted first");
        newCanvas.Modified.Should().BeAfter(dbCanvasPainting.Modified, "Item modified");
        newCanvas.Label.Should().BeEquivalentTo(new LanguageMap("en", "This was inserted ahead of original"));
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItems_ReplaceCanvas_UpdatesDBRecord()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var originalId = new Uri("https://iiif.io/api/first");
        var newId = new Uri("https://iiif.io/api/newly-added");
        var dbCanvasPainting =
            (await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, createdDate: DateTime.UtcNow.AddDays(-1),
                canvasOriginalId: originalId)).Entity;
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{newId}"",
            ""type"": ""Canvas"",
            ""label"": {{ ""en"": [ ""This replaces original"" ] }},
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/2/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                            }},
                            ""target"": ""{newId}""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}", manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == dbManifest.Id);
        fromDatabase.Should().NotBeNull();

        var newCanvas = fromDatabase.CanvasPaintings.Single();
        newCanvas.CanvasOrder.Should().Be(0, "New painting anno inserted first");
        newCanvas.Modified.Should().BeAfter(dbCanvasPainting.Modified, "Item modified");
        newCanvas.Label.Should().BeEquivalentTo(new LanguageMap("en", "This replaces original"));
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItemsWithChoices_AddChoice_UpdatesDBRecord()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var canvasOriginalId = new Uri("https://iiif.io/api/eclipse");
        var canvasId = $"{dbManifest.Id}_canvas";
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest,
            id: canvasId, canvasOriginalId: canvasOriginalId, canvasOrder: 0, choiceOrder: 1, width: 10, height: 10,
            label: new LanguageMap("en", "Original one"));
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{canvasOriginalId}"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""type"": ""Choice"",
                                ""items"": [
                                  {{
                                    ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                    ""type"": ""Image"",
                                    ""format"": ""image/jpeg"",
                                    ""width"": 100,
                                    ""height"": 100,
                                    ""label"": {{ ""en"": [ ""One"" ] }},
                                    ""service"": [
                                      {{
                                        ""@id"": ""https://iiif.org/image/002.jp2"",
                                        ""@type"": ""ImageService2"",
                                        ""profile"": ""http://iiif.io/api/image/2/level1.json""
                                      }}
                                    ]
                                  }},
                                  {{
                                    ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                    ""type"": ""Image"",
                                    ""format"": ""image/jpeg"",
                                    ""width"": 200,
                                    ""height"": 200,
                                    ""label"": {{ ""en"": [ ""Two"" ] }},
                                    ""service"": [
                                      {{
                                        ""@id"": ""https://iiif.org/image/001.jp2"",
                                        ""@type"": ""ImageService2"",
                                        ""profile"": ""http://iiif.io/api/image/2/level1.json""
                                      }}
                                    ]
                                  }}
                                ]
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();

        canvasPaintings.Should().HaveCount(2, "There was 1 initially but 2 in payload");
        
        var canvasPainting1 = canvasPaintings.First();
        canvasPainting1.Id.Should().NotBeNullOrEmpty();
        canvasPainting1.CanvasOriginalId.Should().Be(canvasOriginalId);
        canvasPainting1.CanvasOrder.Should().Be(0);
        canvasPainting1.ChoiceOrder.Should().Be(1);
        canvasPainting1.StaticWidth.Should().Be(100);
        canvasPainting1.StaticHeight.Should().Be(100);
        canvasPainting1.Label.Should().BeEquivalentTo(new LanguageMap("en", "One"));
        
        var canvasPainting2 = canvasPaintings.Last();
        canvasPainting2.Id.Should().Be(canvasPainting1.Id, "Canvas choices share same id");
        canvasPainting2.CanvasOriginalId.Should().Be(canvasOriginalId);
        canvasPainting2.CanvasOrder.Should().Be(0, "Canvas choices share same order");
        canvasPainting2.ChoiceOrder.Should().Be(2);
        canvasPainting2.StaticWidth.Should().Be(200);
        canvasPainting2.StaticHeight.Should().Be(200);
        canvasPainting2.Label.Should().BeEquivalentTo(new LanguageMap("en", "Two"));
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItemsWithChoices_RemoveChoices_UpdatesDBRecord()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var canvasOriginalId = new Uri("https://iiif.io/api/eclipse");
        var canvasId = $"{dbManifest.Id}_canvas";
        var dbCanvasPainting = (await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest,
            id: canvasId, canvasOriginalId: canvasOriginalId, canvasOrder: 0, choiceOrder: 1, width: 10, height: 10,
            label: new LanguageMap("en", "Original one"))).Entity;
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest,
            id: canvasId, canvasOriginalId: canvasOriginalId, canvasOrder: 0, choiceOrder: 2, width: 10, height: 10,
            label: new LanguageMap("en", "Original two"));
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{canvasOriginalId}"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""type"": ""Choice"",
                                ""items"": [
                                  {{
                                    ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                    ""type"": ""Image"",
                                    ""format"": ""image/jpeg"",
                                    ""width"": 200,
                                    ""height"": 200,
                                    ""label"": {{ ""en"": [ ""Two"" ] }},
                                    ""service"": [
                                      {{
                                        ""@id"": ""https://iiif.org/image/001.jp2"",
                                        ""@type"": ""ImageService2"",
                                        ""profile"": ""http://iiif.io/api/image/2/level1.json""
                                      }}
                                    ]
                                  }}
                                ]
                            }},
                            ""target"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();

        canvasPaintings.Should().HaveCount(1);
        
        var canvasPainting = canvasPaintings.Last();
        canvasPainting.Id.Should().Be(dbCanvasPainting.Id);
        canvasPainting.CanvasOriginalId.Should().Be(canvasOriginalId);
        canvasPainting.CanvasOrder.Should().Be(0);
        canvasPainting.ChoiceOrder.Should().Be(1);
        canvasPainting.StaticWidth.Should().Be(200);
        canvasPainting.StaticHeight.Should().Be(200);
        canvasPainting.Label.Should().BeEquivalentTo(new LanguageMap("en", "Two"));
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItems_ReplaceCanvasWithChoices_UpdatesDBRecord()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var canvasOriginalId = new Uri("https://iiif.io/api/eclipse");
        var dbCanvasPainting = (await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest,
            canvasOriginalId: canvasOriginalId, canvasOrder: 0, label: new LanguageMap("en", "Original one"))).Entity;
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{canvasOriginalId}"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/p1/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""type"": ""Choice"",
                                ""items"": [
                                  {{
                                    ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                    ""type"": ""Image"",
                                    ""format"": ""image/jpeg"",
                                    ""width"": 200,
                                    ""height"": 200,
                                    ""label"": {{ ""en"": [ ""Two"" ] }},
                                    ""service"": [
                                      {{
                                        ""@id"": ""https://iiif.org/image/001.jp2"",
                                        ""@type"": ""ImageService2"",
                                        ""profile"": ""http://iiif.io/api/image/2/level1.json""
                                      }}
                                    ]
                                  }}
                                ]
                            }},
                            ""target"": ""{canvasOriginalId}""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();

        canvasPaintings.Should().HaveCount(1);
        
        var canvasPainting = canvasPaintings.Last();
        canvasPainting.Id.Should().Be(dbCanvasPainting.Id);
        canvasPainting.CanvasOriginalId.Should().Be(canvasOriginalId);
        canvasPainting.CanvasOrder.Should().Be(0);
        canvasPainting.ChoiceOrder.Should().Be(1);
        canvasPainting.StaticWidth.Should().Be(200);
        canvasPainting.StaticHeight.Should().Be(200);
        canvasPainting.Label.Should().BeEquivalentTo(new LanguageMap("en", "Two"));
    }

    [Fact]
    public async Task PutFlatId_Update_ExternalItems_RemoveItemFromExistingCanvas_UpdatesDBRecord()
    {
        // Initial state is 3 canvases - simulate removing a new painted anno to this
        var id = TestIdentifiers.Id();
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var canvasOriginalId = new Uri($"https://iiif.io/api/{id}");
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, id: id, canvasOriginalId: canvasOriginalId,
            canvasOrder: 0, label: new LanguageMap("en", "Original one"));
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, id: id, canvasOriginalId: canvasOriginalId,
            canvasOrder: 1, label: new LanguageMap("en", "Original one"));
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, id: id, canvasOriginalId: canvasOriginalId,
            canvasOrder: 2, label: new LanguageMap("en", "Original one"));
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{canvasOriginalId}"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/2/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                            }},
                            ""target"": ""{canvasOriginalId}""
                        }},
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-image/annotation/p0002-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                            }},
                            ""target"": ""{canvasOriginalId}""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}", manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == dbManifest.Id);
        fromDatabase.Should().NotBeNull();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        canvasPaintings.Should().HaveCount(2, "There was 1 initially but 2 in payload");
        
        var canvasPainting1 = canvasPaintings.First();
        canvasPainting1.Id.Should().NotBeNullOrEmpty();
        canvasPainting1.CanvasOriginalId.Should().Be(canvasOriginalId);
        canvasPainting1.CanvasOrder.Should().Be(0);
        canvasPainting1.ChoiceOrder.Should().BeNull();
        
        var canvasPainting2 = canvasPaintings.Last();
        canvasPainting2.Id.Should().Be(canvasPainting1.Id, "Items on same canvas share same id");
        canvasPainting2.CanvasOriginalId.Should().Be(canvasOriginalId);
        canvasPainting2.CanvasOrder.Should().Be(1, "Same canvasId but differing order");
        canvasPainting1.ChoiceOrder.Should().BeNull();
    }
    
    [Fact]
    public async Task PutFlatId_Update_ExternalItems_AddItemToExistingCanvas_UpdatesDBRecord()
    {
        // Initial state is single canvas - simulate adding a new painted anno to this
        var id = TestIdentifiers.Id();
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var canvasOriginalId = new Uri($"https://iiif.io/api/{id}");
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest,
            canvasOriginalId: canvasOriginalId, canvasOrder: 0, label: new LanguageMap("en", "Original one"));
        await dbContext.SaveChangesAsync();
        var slug = dbManifest.Hierarchy.Single().Slug;
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""{canvasOriginalId}"",
            ""type"": ""Canvas"",
            ""height"": 1800,
            ""width"": 1200,
            ""items"": [
                {{
                    ""id"": ""https://iiif.io/api/cookbook/recipe/0001-mvm-image/page/2/1"",
                    ""type"": ""AnnotationPage"",
                    ""items"": [
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-image/annotation/p0001-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                            }},
                            ""target"": ""{canvasOriginalId}""
                        }},
                        {{
                            ""id"": ""https://iiif.io/api/cookbook/recipe/0002-mvm-image/annotation/p0002-image"",
                            ""type"": ""Annotation"",
                            ""motivation"": ""painting"",
                            ""body"": {{
                                ""id"": ""http://iiif.io/api/presentation/2.1/example/fixtures/resources/page2-full.png"",
                                ""type"": ""Image"",
                                ""format"": ""image/png"",
                            }},
                            ""target"": ""{canvasOriginalId}""
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}", manifest, dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == dbManifest.Id);
        fromDatabase.Should().NotBeNull();
        var canvasPaintings = fromDatabase.CanvasPaintings;

        canvasPaintings.Should().HaveCount(2, "There was 1 initially but 2 in payload");
        
        var canvasPainting1 = canvasPaintings.First();
        canvasPainting1.Id.Should().NotBeNullOrEmpty();
        canvasPainting1.CanvasOriginalId.Should().Be(canvasOriginalId);
        canvasPainting1.CanvasOrder.Should().Be(0);
        canvasPainting1.ChoiceOrder.Should().BeNull();
        
        var canvasPainting2 = canvasPaintings.Last();
        canvasPainting2.Id.Should().Be(canvasPainting1.Id, "Items on same canvas share same id");
        canvasPainting2.CanvasOriginalId.Should().Be(canvasOriginalId);
        canvasPainting2.CanvasOrder.Should().Be(1, "Same canvasId but differing order");
        canvasPainting1.ChoiceOrder.Should().BeNull();
    }
    
    [Fact]
    public async Task PutFlatId_ReturnsCanvasPaintingWithRandomId_WhenCanvasIdNotRecognised()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}"",
    ""items"": [
        {{
            ""id"": ""https://localhost:7230/{Customer}/canvases/additionalSlug/{slug}"",
            ""type"": ""Canvas""
        }}
    ]
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        var canvasPainting = responseManifest.PaintedResources.First().CanvasPainting;
        canvasPainting.CanvasId.Split('/').Last().Should().NotBe(slug, "recognised host with unrecognised path");
        canvasPainting.CanvasOriginalId.Should()
            .Be($"https://localhost:7230/{Customer}/canvases/additionalSlug/{slug}");
    }
}
