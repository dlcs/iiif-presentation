using System.Net;
using System.Net.Http.Headers;
using Amazon.S3;
using API.Infrastructure.Helpers;
using API.Infrastructure.Validation;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestUpdateTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 amazonS3;
    private const int Customer = 1;
    
    public ModifyManifestUpdateTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        
        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));
    
        storageFixture.DbFixture.CleanUp();
    }
    
    [Fact]
    public async Task PutFlatId_Update_PreConditionFailed_IfEtagNotProvided()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest();

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson());

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ETagNotMatched");
    }
    
    [Fact]
    public async Task PutFlatId_Update_PreConditionFailed_IfEtagIncorrectProvided()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest();
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson());

        requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"anything\""));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        
        var error = await response.ReadAsPresentationResponseAsync<Error>();
        error!.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ETagNotMatched");
    }
    
    [Fact]
    public async Task PutFlatId_Update_BadRequest_IfParentNotFound()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest(parent: "not-found");
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task PutFlatId_Update_Conflict_IfParentFoundButNotAStorageCollection()
    {
        // Arrange
        var dbCollection = (await dbContext.Collections.AddTestCollection(isStorage: false)).Entity;
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest(parent: dbCollection.Id);
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task PutFlatId_Update_Conflict_IfParentAndSlugAlreadyExist_ForCollection()
    {
        // Arrange
        var dbCollection = (await dbContext.Collections.AddTestCollection()).Entity;
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest(slug: dbCollection.Hierarchy.Single().Slug);
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutFlatId_Update_Conflict_IfParentAndSlug_VaryCase_ForCollection()
    {
        // Arrange
        var dbCollection = (await dbContext.Collections.AddTestCollection()).Entity;
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest(dbCollection.Hierarchy.Single().Slug);
        manifest.Slug = manifest.Slug!.VaryCase();

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task PutFlatId_Update_Conflict_IfParentAndSlugAlreadyExist_ForManifest()
    {
        // Arrange
        var duplicateId = "id_mod_man_upd_tst_pands_ae_fm";
        var duplicateManifest = (await dbContext.Manifests.AddTestManifest(duplicateId)).Entity;
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest(slug: duplicateManifest.Hierarchy.Single().Slug);
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutFlatId_Update_Conflict_IfParentAndSlug_VaryCase_ForManifest()
    {
        // Arrange
        var duplicateId = $"id_{PutFlatId_Update_Conflict_IfParentAndSlug_VaryCase_ForManifest}";
        var duplicateManifest = (await dbContext.Manifests.AddTestManifest(duplicateId)).Entity;
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest(duplicateManifest.Hierarchy.Single().Slug);
        manifest.Slug = manifest.Slug!.VaryCase();

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    public static TheoryData<string> ProhibitedSlugProvider =>
        new(SpecConstants.ProhibitedSlugs);

    [Theory]
    [MemberData(nameof(ProhibitedSlugProvider))]
    public async Task PutFlatId_BadRequest_WhenProhibitedSlug(string slug)
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest($"id_for_slug_{slug}")).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest(slug);

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        var error = await response.ReadAsPresentationResponseAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Detail.Should().Be($"'slug' cannot be one of prohibited terms: '{slug}'");
        error.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/ValidationFailed");
    }
    
    [Fact]
    public async Task PutFlatId_Update_Ok_WhenParentIsDifferentHost()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest =
            dbManifest.ToPresentationManifest(
                parent: $"http://different.host/{dbManifest.CustomerId}/collections/root");
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        responseManifest.Id.Should().NotBeNull();
        responseManifest.Parent.Should().Be($"http://localhost/{Customer}/collections/{RootCollection.Id}");
    }

    [Fact]
    public async Task PutFlatId_Update_UpdatesManifest_ParentIsValidHierarchicalUrl()
    {
        // Arrange
        var createdDate = DateTime.UtcNow.AddDays(-1);
        var dbManifest = (await dbContext.Manifests.AddTestManifest(createdDate: createdDate)).Entity;
        await dbContext.SaveChangesAsync();
        var parent = RootCollection.Id;
        var slug = $"changed_{dbManifest.Hierarchy.Single().Slug}";
        var manifest = dbManifest.ToPresentationManifest(parent: parent, slug: slug);
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.Id.Should().NotBeNull();
        responseManifest.Created.Should().BeCloseTo(createdDate, TimeSpan.FromSeconds(2));
        responseManifest.Modified.Should().BeAfter(createdDate);
        responseManifest.CreatedBy.Should().Be("Admin");
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be($"http://localhost/{Customer}/collections/{RootCollection.Id}");
        responseManifest.PublicId.Should().Be($"http://localhost/1/{slug}",
            "falls back to using host based path generator for customer 1");
        responseManifest.FlatId.Should().Be(dbManifest.Id);
    }
    
    [Fact]
    public async Task PutFlatId_Update_ReturnsManifest()
    {
        // Arrange
        var createdDate = DateTime.UtcNow.AddDays(-1);
        var dbManifest = (await dbContext.Manifests.AddTestManifest(createdDate: createdDate)).Entity;
        await dbContext.SaveChangesAsync();
        var parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}";
        var slug = $"changed_{dbManifest.Hierarchy.Single().Slug}";
        var manifest = dbManifest.ToPresentationManifest(parent: "root", slug: slug);
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest.Id.Should().NotBeNull();
        responseManifest.Created.Should().BeCloseTo(createdDate, TimeSpan.FromSeconds(2));
        responseManifest.Modified.Should().BeAfter(createdDate);
        responseManifest.ModifiedBy.Should().Be("Admin");
        responseManifest.Slug.Should().Be(slug);
        responseManifest.Parent.Should().Be(parent);
    }
    
    [Fact]
    public async Task PutFlatId_Update_UpdatedDBRecord()
    {
        // Arrange
        var createdDate = DateTime.UtcNow.AddDays(-1);
        var dbManifest =
            (await dbContext.Manifests.AddTestManifest(createdDate: createdDate, label: new LanguageMap("en", "foo")))
            .Entity;
        await dbContext.SaveChangesAsync();
        var slug = $"changed_{dbManifest.Hierarchy.Single().Slug}";
        var updatedLabel = new LanguageMap("fr", "foo");
        var manifest =
            dbManifest.ToPresentationManifest(parent: "root", slug: slug, label: updatedLabel);
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fromDatabase = dbContext.Manifests
            .Include(c => c.Hierarchy)
            .Single(c => c.Id == dbManifest.Id);
        var hierarchy = fromDatabase.Hierarchy.Single();

        fromDatabase.Should().NotBeNull();
        hierarchy.Type.Should().Be(ResourceType.IIIFManifest);
        hierarchy.Canonical.Should().BeTrue();
        hierarchy.Slug.Should().Be(slug);
        hierarchy.Parent.Should().Be("root");
        fromDatabase.Created.Should().BeCloseTo(createdDate, TimeSpan.FromSeconds(2));
        fromDatabase.Modified.Should().BeAfter(createdDate);
        fromDatabase.ModifiedBy.Should().Be("Admin");
        fromDatabase.Label.Should().BeEquivalentTo(updatedLabel);
    }
    
    [Fact]
    public async Task PutFlatId_Update_WritesToS3()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest();
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }
    
    [Fact]
    public async Task PutFlatId_Update_IgnoringId()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();
        var manifest = dbManifest.ToPresentationManifest();
        manifest.Id = "https://presentation.example/i-will-be-overwritten";
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var savedS3 =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
                $"{Customer}/manifests/{dbManifest.Id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(dbManifest.Id);
        (s3Manifest.Context as string).Should()
            .Be("http://iiif.io/api/presentation/3/context.json", "Context set automatically");
    }
    
    [Fact]
    public async Task PutFlatId_Update_IgnoresPaintedResources_WhenItemsAndPaintedResourceFilled()
    {
        // Arrange
        var createdDate = DateTime.UtcNow.AddDays(-1);
        var dbManifest = (await dbContext.Manifests.AddTestManifest(createdDate: createdDate)).Entity;
        await dbContext.SaveChangesAsync();
        var slug = $"changed_{dbManifest.Hierarchy.Single().Slug}";
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/1/collections/{RootCollection.Id}",
            Slug = slug,
            Items =
            [
                new Canvas
                {
                    Id = "https://iiif.example/manifestFromItems.json",
                    Items =
                    [
                        new AnnotationPage
                        {
                            Id = "https://iiif.example/manifestFromItemsAnnotationPage.json",
                            Items =
                            [
                                new PaintingAnnotation
                                {
                                    Id = "https://iiif.example/manifestFromItemsPaintingAnnotation.json",
                                    Body = new Canvas
                                    {
                                        Id = "https://iiif.example/manifestFromItemsCanvasBody.json"
                                    }
                                }
                            ]
                        }
                    ]
                }
            ],
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "https://iiif.example/manifest.json"
                    }
                }
            ]
        };
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                manifest.AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var presentationManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        presentationManifest.PaintedResources.Count.Should().Be(1);
        presentationManifest.PaintedResources.First().CanvasPainting.CanvasOriginalId.Should()
            .Be("https://iiif.example/manifestFromItems.json");
        presentationManifest.Items.Count.Should().Be(1);
    }
    
    [Fact]
    public async Task? CreateManifest_ReturnsNiceError_WhenDeserializationError()
    {
        // Arrange
        var slug = nameof(CreateManifest_ReturnsNiceError_WhenDeserializationError);
        var manifest = $$"""
                         {
                           "slug": "{{slug}}",
                           "parent": "https://presentation-api.dlcs.digirati.io/52/collections/root",
                           "type": "Manifest",
                           "label": {
                             "en": [
                               "Example Manifest"
                             ]
                           },
                           "behavior": [
                             "paged"
                           ],
                           "items": [
                             {
                               "my_external_manifest": "https://digirati.io/resources/cheese.json"
                             }
                           ]
                         }
                         """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/brokenManifest",
                manifest);
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        errorResponse!.Detail.Should().Be("Could not deserialize manifest");
        errorResponse.ErrorTypeUri.Should().Be("http://localhost/errors/ModifyCollectionType/CannotDeserialize");
    }
    
    [Fact]
    public async Task UpdateManifest_UpdatesManifest_WhenManifestWithBatchWithoutAssets()
    {
        // Arrange
        var dbManifest =
            (await dbContext.Manifests.AddTestManifest(batchId: 800, ingested: true, canvasPaintings:
            [
                new Models.Database.CanvasPainting
                {
                    Id = "first",
                    CanvasOrder = 1,
                    ChoiceOrder = 1
                }
            ])).Entity;
        await dbContext.SaveChangesAsync();
        var parent = RootCollection.Id;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                dbManifest.ToPresentationManifest(parent: parent).AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task UpdateManifest_UpdatesManifest_WhenExistingManifestNoItemsOrAssets()
    {
        // Arrange
        var dbManifest =
            (await dbContext.Manifests.AddTestManifest(ingested: true)).Entity;
        await dbContext.SaveChangesAsync();
        var parent = RootCollection.Id;
        
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{dbManifest.Id}",
                dbManifest.ToPresentationManifest(parent: parent).AsJson(), dbContext.GetETag(dbManifest));
        
        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
