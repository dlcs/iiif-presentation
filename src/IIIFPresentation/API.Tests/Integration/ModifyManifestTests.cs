using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Models.API.Manifest;
using Models.Database.General;
using Models.Database.Collections;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestTests: IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 amazonS3;
    private const int Customer = 1;

    public ModifyManifestTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
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
            .WithJsonContent("{}");;
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
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", "foo");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_IfInvalid()
    {
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", "{\"id\":\"123");
        
        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_IfParentNotFound()
    {
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
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task CreateManifest_BadRequest_IfParentFoundButNotAStorageCollection()
    {
        var collectionId = nameof(CreateManifest_BadRequest_IfParentFoundButNotAStorageCollection);
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
                    Slug = "update-test",
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
}