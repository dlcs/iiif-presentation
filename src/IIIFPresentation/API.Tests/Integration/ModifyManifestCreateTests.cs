using System.Net;
using System.Net.Http.Headers;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Helpers;
using Core.Infrastructure;
using Core.Response;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using FakeItEasy;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Xunit.Abstractions;
using Collection = Models.Database.Collections.Collection;
using Manifest = Models.Database.Collections.Manifest;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestCreateTests : IClassFixture<PresentationAppFactory<Program>>, IClassFixture<StorageFixture>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 amazonS3;
    private readonly IDlcsApiClient dlcsApiClient;
    private const int Customer = 1;
    private const int InvalidSpaceCustomer = 34512;
    private const int NewlyCreatedSpace = 999;

    public ModifyManifestCreateTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        dlcsApiClient = A.Fake<IDlcsApiClient>();
        A.CallTo(() => dlcsApiClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });
        A.CallTo(() => dlcsApiClient.CreateSpace(InvalidSpaceCustomer, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new DlcsException("Error creating DLCS space", HttpStatusCode.BadRequest));
        httpClient = factory
            .ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
                appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
                services => services.AddSingleton(dlcsApiClient)
            );

        storageFixture.DbFixture.CleanUp();
        if (!dbContext.Collections.Any(i => i.CustomerId == InvalidSpaceCustomer))
        {
            dbContext.Collections.AddTestCollection(RootCollection.Id, customer: InvalidSpaceCustomer).GetAwaiter().GetResult();
            dbContext.SaveChanges();    
        }
    }

    [Fact]
    public async Task CreateManifest_Unauthorized_IfNoAuthTokenProvided()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", "{}");
        
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateManifest_Forbidden_IfIncorrectShowExtraHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{Customer}/manifests")
            .WithJsonContent("{}");
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task CreateManifest_Forbidden_IfNoShowExtraHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{Customer}/manifests")
            .WithJsonContent("{}");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateManifest_BadRequest_IfUnableToDeserialize()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", "foo");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("{\"id\":\"123", "Unterminated string property")]
    [InlineData("{\"id\":\"123\"", "Missing JSON closing bracket")]
    public async Task CreateManifest_BadRequest_IfInvalid(string invalidJson, string because)
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", invalidJson);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because);
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_IfParentNotFound()
    {
        // Arrange
        var manifest = new PresentationManifest
        {
            Parent = "not-found",
            Slug = "balrog"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task CreateManifest_Conflict_IfParentFoundButNotAStorageCollection()
    {
        // Arrange
        var (slug, collectionId) = TestIdentifiers.SlugResource();
        
        var initialCollection = new Collection
        {
            Id = collectionId,
            UsePath = true,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = false,
            CustomerId = 1,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{collectionId}",
            Slug = "balrog"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task CreateManifest_Conflict_IfParentAndSlugExist_ForCollection()
    {
        // Arrange
        var (slug, collectionId) = TestIdentifiers.SlugResource();
        
        var duplicateCollection = new Collection
        {
            Id = collectionId,
            UsePath = true,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            IsStorageCollection = false,
            CustomerId = 1,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };
        
        await dbContext.Collections.AddAsync(duplicateCollection);
        await dbContext.SaveChangesAsync();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task CreateManifest_Conflict_IfParentAndSlugExist_ForManifest()
    {
        // Arrange
        var (slug, collectionId) = TestIdentifiers.SlugResource();
        
        var duplicateManifest = new Manifest
        {
            Id = collectionId,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            CustomerId = 1,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };
        
        await dbContext.AddAsync(duplicateManifest);
        await dbContext.SaveChangesAsync();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_WhenParentIsInvalidHierarchicalUri()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = new PresentationManifest
        {
            Parent = "http://different.host/root",
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("The parent collection could not be found");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ParentCollectionNotFound");
    }

    public static TheoryData<string> ProhibitedSlugProvider =>
        new(SpecConstants.ProhibitedSlugs);

    [Theory]
    [MemberData(nameof(ProhibitedSlugProvider))]
    public async Task CreateManifest_BadRequest_WhenProhibitedSlug(string slug)
    {
        // Arrange
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be($"'slug' cannot be one of prohibited terms: '{slug}'");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ValidationFailed");
    }
    
    [Fact]
    public async Task CreateManifest_InternalServerError_IfSpaceRequested_ButFails()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{InvalidSpaceCustomer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}""
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{InvalidSpaceCustomer}/manifests", manifest);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer(InvalidSpaceCustomer).SendAsync(requestMessage);

        // Assert
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        error!.Detail.Should().Be("Error creating DLCS space");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/DlcsError");
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifest_ParentIsValidHierarchicalUrl()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
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
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifest_WhileRemovingPresentationBehaviors()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Behavior = [
                Behavior.IsPublic,
                "custom-behavior"
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest.Id.Should().NotBeNull();
        responseManifest.Behavior.Should().OnlyContain(x => x == "custom-behavior");
    }
    
    [Fact]
    public async Task CreateManifest_ReturnsManifest()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest =
            $$"""

               {
                   "@context": "http://iiif.io/api/presentation/3/context.json",
                   "id": "https://iiif.example/manifest.json",
                   "type": "Manifest",
                   "parent": "http://localhost/{{Customer}}/collections/{{RootCollection.Id}}",
                   "slug": "{{slug}}"
               }
               """;
        
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
        responseManifest.PaintedResources.Should().BeNullOrEmpty();
        responseManifest.Space.Should().BeNull("No space was requested");
    }
    
    [Fact]
    public async Task CreateManifest_ReturnsManifestWithEtag_WhenLinkHeaderNoAssets()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest =
            $$"""

              {
                  "@context": "http://iiif.io/api/presentation/3/context.json",
                  "id": "https://iiif.example/manifest.json",
                  "type": "Manifest",
                  "parent": "http://localhost/{{Customer}}/collections/{{RootCollection.Id}}",
                  "slug": "{{slug}}"
              }
              """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest);
        HttpRequestMessageBuilder.AddLinkHeader(requestMessage);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest.Id.Should().NotBeNull();
        responseManifest.CreatedBy.Should().Be("Admin");
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
        responseManifest.PaintedResources.Should().BeNullOrEmpty();
        responseManifest.Space.Should().NotBeNull();
        
        var dbManifest = dbContext.Manifests.First(x => x.Id == responseManifest.Id.GetLastPathElement());
        dbManifest.Etag.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task CreateManifest_CreatedDBRecord()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        var hierarchy = fromDatabase.Hierarchy.Single();

        fromDatabase.Should().NotBeNull();
        fromDatabase.SpaceId.Should().BeNull("No space was requested");
        fromDatabase.LastProcessed.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();
    }
    
    [Fact]
    public async Task CreateManifest_IfSpaceRequested_ReturnsManifest()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest =
            $$"""
              {
                  "@context": "http://iiif.io/api/presentation/3/context.json",
                  "id": "https://iiif.example/manifest.json",
                  "type": "Manifest",
                  "parent": "http://localhost/{{Customer}}/collections/{{RootCollection.Id}}",
                  "slug": "{{slug}}"
              }
              """;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.Space.Should().Be("https://localhost:6000/customers/1/spaces/999");
    }
    
    [Fact]
    public async Task CreateManifest_IfSpaceRequested_CreatedDBRecord()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var fromDatabase = dbContext.Manifests
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        fromDatabase.SpaceId.Should().Be(NewlyCreatedSpace);
    }
    
    [Fact]
    public async Task CreateManifest_WithLabel_SavesLabel()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();

        var label = new LanguageMap("en", "illinoise");
        label.Add("none", ["nope"]);
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
            Label = label
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();
        
        var fromDatabase = dbContext.Manifests.Single(c => c.Id == id);
        fromDatabase.Label.Should().BeEquivalentTo(label);
    }
    
    [Fact]
    public async Task CreateManifest_NoAssets_WritesToS3_Real()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/manifests/{id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(id);
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }

    [Fact]
    public async Task CreateManifest_WithAsset_WritesToS3_Staging()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
            PaintedResources =
            [
                new()
                {
                    Asset = new(new JProperty("id", assetId))
                }
            ]
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"staging/{Customer}/manifests/{id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(id);
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }
    
    [Fact]
    public async Task CreateManifest_WritesToS3_IgnoringId()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
            Id = "https://presentation.example/i-will-be-overwritten"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/manifests/{id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(id);
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }

    [Fact]
    public async Task CreateManifest_ReturnsManifest_WithProvisionalItems()
    {
        var slug = TestIdentifiers.Slug();

        #region ManifestJson

        var manifestJson =
            $$"""
                  {
                  "type": "Manifest",
                  "behavior": [
                      "public-iiif"
                  ],
                  "label": {
                      "en": [
                          "Testing"
                      ]
                  },
                  "slug": "{{slug}}",
                  "parent": "http://localhost/{{Customer}}/collections/{{RootCollection.Id}}",
                  "paintedResources": [
                      {
                          "canvasPainting":{
                              "canvasOrder": 1,
                              "choiceOrder": 1
                          },
                          "asset": {
                              "id": "test-AssetByPresentation-multiple-1",
                              "mediaType": "image/png",
                              "space": {{NewlyCreatedSpace}},
                              "origin": "https://example.com/customers/34/space/22/1.png",
                              "deliveryChannels": [
                                  {
                                      "channel": "iiif-img",
                                      "policy": "default"
                                  },
                                  {
                                      "channel": "thumbs",
                                      "policy": "default"
                                  }
                              ]
                          }
                      },
                      {
                          "canvasPainting":{
                              "canvasOrder": 1,
                              "choiceOrder": 2
                          },
                          "asset": {
                              "id": "test-AssetByPresentation-multiple-2",
                              "mediaType": "image/png",
                              "space": {{NewlyCreatedSpace}},
                              "origin": "https://example.com/customers/34/space/22/2.png",
                              "deliveryChannels": [
                                  {
                                      "channel": "iiif-img",
                                      "policy": "default"
                                  },
                                  {
                                      "channel": "thumbs",
                                      "policy": "default"
                                  }
                              ]
                          }
                      },
                      {
                          "canvasPainting":{
                              "canvasOrder": 2
                          },
                          "asset": {
                              "id": "test-AssetByPresentation-multiple-3",
                              "mediaType": "image/png",
                              "space": {{NewlyCreatedSpace}},
                              "origin": "https://example.com/customers/34/space/22/3.png",
                              "deliveryChannels": [
                                  {
                                      "channel": "iiif-img",
                                      "policy": "default"
                                  },
                                  {
                                      "channel": "thumbs",
                                      "policy": "default"
                                  }
                              ]
                          }
                      }
                  ] 
              }             
              """;

        #endregion
        
            var requestMessage =
                HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifestJson);

            // Act
            var response = await httpClient.AsCustomer().SendAsync(requestMessage);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);

            var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
            responseCollection.Should().NotBeNull("valid manifest is expected");
            responseCollection!.Slug.Should().Be(slug, "it should remain unchanged");
            responseCollection.Items.Should().NotBeNull("we expect provisional items to be filled");
            responseCollection.Items!.Should()
                .HaveCount(2, "there are 3 images, but two of them are choices under one canvas");
            var firstCanvas = responseCollection.Items!.First();
            firstCanvas.Type.Should().Be("Canvas");
            firstCanvas.Items.Should().NotBeNull("Canvas should have AnnotationPage");
            var firstAnnotationPage = firstCanvas.Items!.First();
            firstAnnotationPage.Type.Should().Be("AnnotationPage");
            firstAnnotationPage.Items.Should().NotBeNull("AnnotationPage should have Annotation");
            var firstAnnotation = firstAnnotationPage.Items!.First();
            firstAnnotation.Should().BeOfType<PaintingAnnotation>();
            var firstBody = (firstAnnotation as PaintingAnnotation)!.Body;
            firstBody.Should().BeOfType<PaintingChoice>();
    }
    
    [Fact]
    public async Task PutFlatId_Unauthorized_IfNoAuthTokenProvided()
    {
        // Arrange
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/dolphin", "{}");
        
        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutFlatId_Forbidden_IfIncorrectShowExtraHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"{Customer}/manifests/dolphin")
            .WithJsonContent("{}");
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task PutFlatId_Forbidden_IfNoShowExtraHeader()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"{Customer}/manifests/dolphin")
            .WithJsonContent("{}");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    
    [Fact]
    public async Task PutFlatId_BadRequest_IfUnableToDeserialize()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/dolphin", "foo");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("{\"id\":\"123", "Unterminated string property")]
    [InlineData("{\"id\":\"123\"", "Missing JSON closing bracket")]
    public async Task PutFlatId_BadRequest_IfInvalid(string invalidJson, string because)
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/dolphin", invalidJson);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_PreConditionFailed_IfEtagProvided()
    {
        const string id = nameof(PutFlatId_Insert_PreConditionFailed_IfEtagProvided);
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/not-found",
            Slug = "balrog"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest.AsJson());

        requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"anything\""));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ETagNotAllowed");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentFoundButNotAStorageCollection()
    {
        // Arrange
        var (slug, collectionId) = TestIdentifiers.SlugResource();
        
        var initialCollection = new Collection
        {
            Id = collectionId,
            UsePath = true,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = false,
            CustomerId = 1,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };
        
        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{collectionId}",
            Slug = "balrog"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentAndSlugExist_ForCollection()
    {
        // Arrange
        var (slug, collectionId) = TestIdentifiers.SlugResource();
        
        var duplicateCollection = new Collection
        {
            Id = collectionId,
            UsePath = true,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            IsStorageCollection = false,
            CustomerId = 1,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };
        
        await dbContext.Collections.AddAsync(duplicateCollection);
        await dbContext.SaveChangesAsync();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentAndSlug_VaryCase_ForCollection()
    {
        // Arrange
        var (slug, collectionId) = TestIdentifiers.SlugResource();
        
        var duplicateCollection = new Collection
        {
            Id = collectionId,
            UsePath = true,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            IsStorageCollection = false,
            CustomerId = 1,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };

        await dbContext.Collections.AddAsync(duplicateCollection);
        await dbContext.SaveChangesAsync();

        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug.VaryCase()
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentAndSlugExist_ForManifest()
    {
        // Arrange
        var (slug, collectionId) = TestIdentifiers.SlugResource();
        
        var duplicateManifest = new Manifest
        {
            Id = collectionId,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            CustomerId = 1,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };
        
        await dbContext.AddAsync(duplicateManifest);
        await dbContext.SaveChangesAsync();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentAndSlug_VaryCase_ForManifest()
    {
        // Arrange
        var (slug, collectionId) = TestIdentifiers.SlugResource();
        
        var duplicateManifest = new Manifest
        {
            Id = collectionId,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            CustomerId = 1,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = slug,
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };

        await dbContext.AddAsync(duplicateManifest);
        await dbContext.SaveChangesAsync();

        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug.VaryCase()
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_BadRequest_WhenParentIsInvalidHierarchicalUri()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = new PresentationManifest
        {
            Parent = "http://different.host/root",
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("The parent collection could not be found");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ParentCollectionNotFound");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_InternalServerError_IfSpaceRequested_ButFails()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{InvalidSpaceCustomer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}""
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{InvalidSpaceCustomer}/manifests/foo", manifest);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer(InvalidSpaceCustomer).SendAsync(requestMessage);

        // Assert
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        error!.Detail.Should().Be("Error creating DLCS space");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/DlcsError");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_CreatesManifest_ParentIsValidHierarchicalUrl()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest.AsJson());
        
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
        responseManifest.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_ReturnsManifest()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}""
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
        responseManifest.PaintedResources.Should().BeNullOrEmpty();
        responseManifest.PublicId.Should().Be($"http://localhost/1/{slug}");
        responseManifest.FlatId.Should().Be(id);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_CreatedDBRecord()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}""
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

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();
        fromDatabase.CanvasPaintings.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public async Task PutFlatId_Insert_IfSpaceRequested_ReturnsManifest()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}""
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.Space.Should().Be("https://localhost:6000/customers/1/spaces/999");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_IfSpaceRequested_CreatedDBRecord()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""http://localhost/{Customer}/collections/{RootCollection.Id}"",
    ""slug"": ""{slug}""
}}";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest);
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var fromDatabase = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == id);
        fromDatabase.SpaceId.Should().Be(NewlyCreatedSpace);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_NoAssets_WritesToS3()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/manifests/{id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(id);
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }

    [Fact]
    public async Task PutFlatId_Insert_WithAsset_WritesToS3()
    {
        // Arrange
        var (slug, id,  assetId) = TestIdentifiers.SlugResourceAsset();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
            PaintedResources =
            [
                new()
                {
                    Asset = new(new JProperty("id", assetId))
                }
            ]
        };


        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"staging/{Customer}/manifests/{id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(id);
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_WritesToS3_IgnoringId()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();

        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
            Id = "https://presentation.example/i-will-be-overwritten"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/manifests/{id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(id);
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifestWithSpecifiedCanvasId_WhenCanvasIdFilledIn()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();
        var (_, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Behavior = [
                Behavior.IsPublic
            ],
            Slug = slug,
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = canvasPaintingId
                    },
                    Asset = new JObject
                    {
                        ["id"] = assetId,
                        ["mediaType"] = "image/jpeg"
                    },
                }
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var presentationManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        presentationManifest.PaintedResources.Count.Should().Be(1);
        presentationManifest.PaintedResources.First().CanvasPainting.CanvasId.Should()
            .Be($"http://localhost/{Customer}/canvases/{canvasPaintingId}");
        presentationManifest.Items.Count.Should().Be(1);
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifestWithSpecifiedCanvasId_WhenCanvasIdFilledInLongform()
    {
        // Arrange
        var (slug, assetId) = TestIdentifiers.SlugResource();
        var (_, canvasPaintingId) = TestIdentifiers.IdCanvasPainting();
        
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Behavior = [
                Behavior.IsPublic
            ],
            Slug = slug,
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = $"https://iiif.io/{Customer}/canvases/{canvasPaintingId}"
                    },
                    Asset = new JObject
                    {
                        ["id"] = assetId,
                        ["mediaType"] = "image/jpeg"
                    },
                }
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var presentationManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        presentationManifest.PaintedResources.Count.Should().Be(1);
        presentationManifest.PaintedResources.First().CanvasPainting.CanvasId.Should()
            .Be($"http://localhost/{Customer}/canvases/{canvasPaintingId}");
        presentationManifest.Items.Count.Should().Be(1);
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_WhenCanvasIdIncorrectFormat()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Behavior = [
                Behavior.IsPublic
            ],
            Slug = slug,
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = TestIdentifiers.IdCanvasPainting().canvasPaintingId + "/incorrect"
                    },
                    Asset = new JObject
                    {
                        ["id"] = "1b",
                        ["mediaType"] = "image/jpeg"
                    },
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
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/InvalidCanvasId");
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_WhenCanvasIdNotDuplicatedInCanvasOrder()
    {
        // Arrange
        var slug = TestIdentifiers.Id();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug,
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = $"https://iiif.io/{Customer}/canvases/different-1",
                        CanvasOrder = 1,
                        ChoiceOrder = 1
                    },
                    Asset = new JObject
                    {
                        ["id"] = "1b",
                        ["mediaType"] = "image/jpeg"
                    },
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "different-2",
                        CanvasOrder = 1,
                        ChoiceOrder = 2
                    },
                    Asset = new JObject
                    {
                        ["id"] = "1b",
                        ["mediaType"] = "image/jpeg"
                    },
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
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ValidationFailed");
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifestWithSpecifiedCanvasId_WhenCanvasIdFilledInForChoice()
    {
        // Arrange
        var slug = TestIdentifiers.Slug();
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Behavior = [
                Behavior.IsPublic
            ],
            Slug = slug,
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "manifestFromPainted",
                        CanvasOrder = 1,
                        ChoiceOrder = 1
                    },
                    Asset = new JObject
                    {
                        ["id"] = "1b",
                        ["mediaType"] = "image/jpeg"
                    },
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "manifestFromPainted",
                        CanvasOrder = 1,
                        ChoiceOrder = 2
                    },
                    Asset = new JObject
                    {
                        ["id"] = "1b",
                        ["mediaType"] = "image/jpeg"
                    },
                }
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var presentationManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        presentationManifest.PaintedResources.Count.Should().Be(2);
        presentationManifest.PaintedResources.First().CanvasPainting.CanvasId.Should()
            .Be($"http://localhost/{Customer}/canvases/manifestFromPainted");
        presentationManifest.PaintedResources.Last().CanvasPainting.CanvasId.Should()
            .Be($"http://localhost/{Customer}/canvases/manifestFromPainted");
        presentationManifest.Items.Count.Should().Be(1);
    }
}
