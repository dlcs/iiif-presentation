using System.Net;
using System.Net.Http.Headers;
using Amazon.S3;
using API.Infrastructure.Validation;
using API.Tests.Integration.Infrastructure;
using Core.Helpers;
using Core.Response;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Collection = Models.Database.Collections.Collection;
using Manifest = Models.Database.Collections.Manifest;

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
    private readonly IAmazonS3 amazonS3;
    private const int Customer = 1;

    public ModifyManifestExternalItemsTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task CreateManifest_ExternalItems_ReturnsManifest()
    {
        // Arrange
        var slug = nameof(CreateManifest_ExternalItems_ReturnsManifest);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var slug = nameof(CreateManifest_ExternalItems_CreatedDBRecord);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var slug = nameof(CreateManifest_ExternalItems_MultipleCanvases_CreatedDBRecord);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var slug = nameof(CreateManifest_ExternalItemsWithChoices_CreatedDBRecord);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
    public async Task PutFlatId_Insert_ExternalItems_ReturnsManifest()
    {
        // Arrange
        var slug = $"slug_{nameof(PutFlatId_Insert_ExternalItems_ReturnsManifest)}";
        var id = nameof(PutFlatId_Insert_ExternalItems_ReturnsManifest);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        responseManifest.PublicId.Should().Be($"http://localhost/1/{slug}");
        responseManifest.FlatId.Should().Be(id);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_ExternalItems_CreatedDBRecord()
    {
        // Arrange
        var slug = $"slug_{nameof(PutFlatId_Insert_ExternalItems_CreatedDBRecord)}";
        var id = nameof(PutFlatId_Insert_ExternalItems_CreatedDBRecord);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        canvasPainting.CanvasOriginalId.Should().Be("https://iiif.example/slug_PutFlatId_Insert_ExternalItems_CreatedDBRecord.json");
    }
}