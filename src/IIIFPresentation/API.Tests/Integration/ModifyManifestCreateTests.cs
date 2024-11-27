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

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestCreateTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 amazonS3;
    private const int Customer = 1;

    public ModifyManifestCreateTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        storageFixture.DbFixture.CleanUp();
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task CreateManifest_Conflict_IfParentFoundButNotAStorageCollection()
    {
        // Arrange
        var collectionId = nameof(CreateManifest_Conflict_IfParentFoundButNotAStorageCollection);
        var slugId = $"slug_{nameof(CreateManifest_Conflict_IfParentFoundButNotAStorageCollection)}";
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
                    Slug = slugId,
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
            Parent = collectionId,
            Slug = "balrog"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task CreateManifest_Conflict_IfParentAndSlugExist_ForCollection()
    {
        // Arrange
        var collectionId = nameof(CreateManifest_Conflict_IfParentAndSlugExist_ForCollection);
        var slug = $"slug_{nameof(CreateManifest_Conflict_IfParentAndSlugExist_ForCollection)}";
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
            Parent = RootCollection.Id,
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task CreateManifest_Conflict_IfParentAndSlugExist_ForManifest()
    {
        // Arrange
        var collectionId = nameof(CreateManifest_Conflict_IfParentAndSlugExist_ForManifest);
        var slug = $"slug_{nameof(CreateManifest_Conflict_IfParentAndSlugExist_ForManifest)}";
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
            Parent = RootCollection.Id,
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_WhenParentIsInvalidHierarchicalUri()
    {
        // Arrange
        var slug = nameof(CreateManifest_BadRequest_WhenParentIsInvalidHierarchicalUri);
        var manifest = new PresentationManifest
        {
            Parent = "http://different.host/root",
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be($"'slug' cannot be one of prohibited terms: '{slug}'");
        error.ErrorTypeUri.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
    }
    
    [Fact]
    public async Task CreateManifest_CreatesManifest_ParentIsValidHierarchicalUrl()
    {
        // Arrange
        var slug = nameof(CreateManifest_CreatesManifest_ParentIsValidHierarchicalUrl);
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
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
    }
    
    [Fact]
    public async Task CreateManifest_ReturnsManifest()
    {
        // Arrange
        var slug = nameof(CreateManifest_ReturnsManifest);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
    ""slug"": ""{slug}""
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
        responseManifest.PaintedResources.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public async Task CreateManifest_WithLabel_SavesLabel()
    {
        // Arrange
        var slug = nameof(CreateManifest_WithLabel_SavesLabel);

        var label = new LanguageMap("en", "illinoise");
        label.Add("none", ["nope"]);
        
        var manifest = new PresentationManifest
        {
            Parent = RootCollection.Id,
            Slug = slug,
            Label = label
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();
        
        var fromDatabase = dbContext.Manifests.Single(c => c.Id == id);
        fromDatabase.Label.Should().BeEquivalentTo(label);
    }
    
    [Fact]
    public async Task CreateManifest_WritesToS3()
    {
        // Arrange
        var slug = nameof(CreateManifest_WritesToS3);
        var manifest = new PresentationManifest
        {
            Parent = RootCollection.Id,
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
    public async Task CreateManifest_WritesToS3_IgnoringId()
    {
        // Arrange
        var slug = nameof(CreateManifest_WritesToS3_IgnoringId);
        var manifest = new PresentationManifest
        {
            Parent = RootCollection.Id,
            Slug = slug,
            Id = "https://presentation.example/i-will-be-overwritten"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
            .WithJsonContent("{}");;
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_PreConditionFailed_IfEtagProvided()
    {
        const string id = nameof(PutFlatId_Insert_PreConditionFailed_IfEtagProvided);
        var manifest = new PresentationManifest
        {
            Parent = "not-found",
            Slug = "balrog"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest.AsJson());

        requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"anything\""));

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ETagNotAllowed");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentFoundButNotAStorageCollection()
    {
        // Arrange
        var collectionId = nameof(PutFlatId_Insert_Conflict_IfParentFoundButNotAStorageCollection);
        var slug = $"s_{nameof(PutFlatId_Insert_Conflict_IfParentFoundButNotAStorageCollection)}";
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
            Parent = collectionId,
            Slug = "balrog"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentAndSlugExist_ForCollection()
    {
        // Arrange
        var collectionId = nameof(PutFlatId_Insert_Conflict_IfParentAndSlugExist_ForCollection);
        var slug = $"slug_{nameof(PutFlatId_Insert_Conflict_IfParentAndSlugExist_ForCollection)}";
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
            Parent = RootCollection.Id,
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentAndSlug_VaryCase_ForCollection()
    {
        // Arrange
        var collectionId = nameof(PutFlatId_Insert_Conflict_IfParentAndSlug_VaryCase_ForCollection);
        var slug = $"slug_{nameof(PutFlatId_Insert_Conflict_IfParentAndSlug_VaryCase_ForCollection)}";
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
            Parent = RootCollection.Id,
            Slug = slug.VaryCase()
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentAndSlugExist_ForManifest()
    {
        // Arrange
        var collectionId = nameof(PutFlatId_Insert_Conflict_IfParentAndSlugExist_ForManifest);
        var slug = $"slug_{nameof(PutFlatId_Insert_Conflict_IfParentAndSlugExist_ForManifest)}";
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
            Parent = RootCollection.Id,
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutFlatId_Insert_Conflict_IfParentAndSlug_VaryCase_ForManifest()
    {
        // Arrange
        var collectionId = nameof(PutFlatId_Insert_Conflict_IfParentAndSlug_VaryCase_ForManifest);
        var slug = $"slug_{nameof(PutFlatId_Insert_Conflict_IfParentAndSlug_VaryCase_ForManifest)}";
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
            Parent = RootCollection.Id,
            Slug = slug.VaryCase()
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_BadRequest_WhenParentIsInvalidHierarchicalUri()
    {
        // Arrange
        var slug = nameof(PutFlatId_Insert_BadRequest_WhenParentIsInvalidHierarchicalUri);
        var manifest = new PresentationManifest
        {
            Parent = "http://different.host/root",
            Slug = slug
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/foo", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be("The parent collection could not be found");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ParentCollectionNotFound");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_CreatesManifest_ParentIsValidHierarchicalUrl()
    {
        // Arrange
        var id = nameof(PutFlatId_Insert_CreatesManifest_ParentIsValidHierarchicalUrl);
        var slug = $"s_{nameof(PutFlatId_Insert_CreatesManifest_ParentIsValidHierarchicalUrl)}";
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest.AsJson());
        
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
        responseManifest.Parent.Should().Be($"http://localhost/1/collections/{RootCollection.Id}");
    }
    
    [Fact]
    public async Task PutFlatId_Insert_ReturnsManifest()
    {
        // Arrange
        var slug = $"slug_{nameof(PutFlatId_Insert_ReturnsManifest)}";
        var id = nameof(PutFlatId_Insert_ReturnsManifest);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
    ""slug"": ""{slug}""
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
        responseManifest.PaintedResources.Should().BeNullOrEmpty();
        responseManifest.PublicId.Should().Be($"http://localhost/1/{slug}");
        responseManifest.FlatId.Should().Be(id);
    }
    
    [Fact]
    public async Task PutFlatId_Insert_CreatedDBRecord()
    {
        // Arrange
        var slug = $"slug_{nameof(PutFlatId_Insert_CreatedDBRecord)}";
        var id = nameof(PutFlatId_Insert_CreatedDBRecord);
        var manifest = $@"
{{
    ""@context"": ""http://iiif.io/api/presentation/3/context.json"",
    ""id"": ""https://iiif.example/manifest.json"",
    ""type"": ""Manifest"",
    ""parent"": ""{RootCollection.Id}"",
    ""slug"": ""{slug}""
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

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();
        fromDatabase.CanvasPaintings.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public async Task PutFlatId_Insert_WritesToS3()
    {
        // Arrange
        var slug = $"slug_{nameof(PutFlatId_Insert_WritesToS3)}";
        var id = nameof(PutFlatId_Insert_WritesToS3);
        var manifest = new PresentationManifest
        {
            Parent = RootCollection.Id,
            Slug = slug,
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
    public async Task PutFlatId_Insert_WritesToS3_IgnoringId()
    {
        // Arrange
        var slug = $"slug_{nameof(PutFlatId_Insert_WritesToS3_IgnoringId)}";
        var id = nameof(PutFlatId_Insert_WritesToS3_IgnoringId);
        var manifest = new PresentationManifest
        {
            Parent = RootCollection.Id,
            Slug = slug,
            Id = "https://presentation.example/i-will-be-overwritten"
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}", manifest.AsJson());
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

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
}