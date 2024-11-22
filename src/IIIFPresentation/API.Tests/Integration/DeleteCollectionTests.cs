﻿using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Helpers;
using Core.Response;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Models.Infrastucture;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class DeleteCollectionTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;

    private readonly PresentationContext dbContext;

    private readonly IAmazonS3 amazonS3;

    private const int Customer = 1;

    public DeleteCollectionTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task DeleteCollection_ReturnsUnauthorized_WhenCalledWithoutAuth()
    {
        // Arrange
        var collectionName = nameof(DeleteCollection_ReturnsUnauthorized_WhenCalledWithoutAuth);

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, $"{Customer}/collections/{collectionName}");

        // Act
        var response = await httpClient.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader()
    {
        // Arrange
        var collectionName = nameof(DeleteCollection_ReturnsForbidden_WhenCalledWithIncorrectShowExtraHeader);

        var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{Customer}/collections/{collectionName}");
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "Incorrect");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCollection_DeletesCollection_WhenAllValuesProvided()
    {
        // Arrange
        var initialCollection = new Collection()
        {
            Id = "DeleteTester",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", new() {"update testing"}}
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = false,
            CustomerId = 1
        };

        await dbContext.Hierarchy.AddAsync(new Hierarchy
        {
            CollectionId = "DeleteTester",
            Slug = "delete-test",
            Parent = RootCollection.Id,
            Type = ResourceType.StorageCollection,
            CustomerId = 1,
            Canonical = true
        });

        await dbContext.Collections.AddAsync(initialCollection);
        await dbContext.SaveChangesAsync();

        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/{initialCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var fromDatabase = dbContext.Collections.FirstOrDefault(c => c.Id == initialCollection.Id);
        var fromDatabaseHierarchy = dbContext.Hierarchy.FirstOrDefault(c => c.CollectionId == initialCollection.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        fromDatabase.Should().BeNull();
        fromDatabaseHierarchy.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCollection_FailsToDeleteCollection_WhenNotFound()
    {
        // Arrange
        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/doesNotExist");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCollection_FailsToDeleteCollection_WhenAttemptingToDeleteRoot()
    {
        // Arrange
        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errorResponse!.ErrorTypeUri.Should()
            .Be("http://localhost/errors/DeleteCollectionType/CannotDeleteRootCollection");
        errorResponse.Detail.Should().Be("Cannot delete a root collection");
    }


    [Fact]
    public async Task DeleteCollection_FailsToDeleteCollection_WhenAttemptingToDeleteRootDirectly()
    {
        // Arrange
        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/{RootCollection.Id}");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errorResponse!.ErrorTypeUri.Should()
            .Be("http://localhost/errors/DeleteCollectionType/CannotDeleteRootCollection");
        errorResponse.Detail.Should().Be("Cannot delete a root collection");
    }

    [Fact]
    public async Task DeleteCollection_FailsToDeleteCollection_WhenAttemptingToDeleteCollectionWithItems()
    {
        // Arrange
        var deleteRequestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/collections/FirstChildCollection");

        // Act
        var response = await httpClient.AsCustomer(1).SendAsync(deleteRequestMessage);

        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errorResponse!.ErrorTypeUri.Should().Be("http://localhost/errors/DeleteCollectionType/CollectionNotEmpty");
        errorResponse.Detail.Should().Be("Cannot delete a collection with child items");
    }
    
    [Fact]
    public async Task DeleteIiifCollection_DeletesInS3()
    {
        // Arrange
        var slug = nameof(DeleteIiifCollection_DeletesInS3);
        var collection = new PresentationCollection
        {
            Behavior = new List<string>()
            {
                Behavior.IsPublic
            },
            Parent = RootCollection.Id,
            Slug = slug
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
                collection.AsJson());

        var response = await httpClient.AsCustomer(1).SendAsync(requestMessage);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();
        var id = responseCollection!.Id.GetLastPathElement();
        
        var objectInS3 = await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
            $"{Customer}/collections/{id}");
        objectInS3.Should().NotBeNull();

        requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, $"{Customer}/collections/{id}");


        // Act
        response = await httpClient.AsCustomer(1).SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await new Func<Task>(async () => await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
            $"{Customer}/collections/{id}")).Should().ThrowAsync<AmazonS3Exception>();
    }
}