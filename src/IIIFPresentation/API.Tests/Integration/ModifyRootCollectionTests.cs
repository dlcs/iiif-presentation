using System.Net;
using System.Net.Http.Headers;
using Amazon.S3;
using API.Infrastructure.Helpers;
using API.Tests.Integration.Infrastructure;
using Core.Infrastructure;
using FakeItEasy;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models;
using Models.API.Collection;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ModifyRootCollectionTests: IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 s3Client = A.Fake<IAmazonS3>();

    public ModifyRootCollectionTests(PresentationContextFixture dbFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = dbFixture.DbContext;

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(dbFixture,
            additionalTestServices: collection => collection.AddSingleton(s3Client));
        
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Put_400_IfTryToModifySlug()
    {
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "not-root"
        };
        
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{PresentationContextFixture.CustomerId}/collections/{RootCollection.Id}", collection.AsJson(),dbContext.GetETagById(1, RootCollection.Id));
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Unable to change root slug");
    }
    
    [Fact]
    public async Task Put_400_IfTryToModifyParent()
    {
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["test collection"]),
            Slug = "",
            Parent = "not-root",
        };
        
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{PresentationContextFixture.CustomerId}/collections/{RootCollection.Id}", collection.AsJson(),dbContext.GetETagById(1, RootCollection.Id));
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Unable to change root slug");
    }
    
    [Fact]
    public async Task Put_400_IfTryToChangeFromStorageCollection()
    {
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
            ],
            Label = new LanguageMap("en", ["test collection"]),
        };
        
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{PresentationContextFixture.CustomerId}/collections/{RootCollection.Id}", collection.AsJson(),dbContext.GetETagById(1, RootCollection.Id));
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Unable to change root slug");
    }
    
    [Fact]
    public async Task Put_412_IfNoEtagProvided()
    {
        var collection = new PresentationCollection
        {
            Behavior = [Behavior.IsStorageCollection,],
            Label = new LanguageMap("en", ["test collection"]),
        };
        
        // NOTE - no etag
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{PresentationContextFixture.CustomerId}/collections/{RootCollection.Id}", collection.AsJson());
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed, "No eTag provided");
    }
    
    [Fact]
    public async Task Put_412_IfIncorrectEtagProvided()
    {
        var collection = new PresentationCollection
        {
            Behavior = [Behavior.IsStorageCollection,],
            Label = new LanguageMap("en", ["test collection"]),
        };
        
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{PresentationContextFixture.CustomerId}/collections/{RootCollection.Id}", collection.AsJson());
        requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue("\"lightspeed\""));
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed, "Incorrect eTag provided");
    }

    [Fact]
    public async Task Put_CanChangeLabel()
    {
        const int customer = 1234892;
        var dbCollection = await dbContext.Collections.AddTestCollection(KnownCollections.RootCollection, customer, slug: "root", parent: null);
        await dbContext.SaveChangesAsync();
        
        const string newLabel = "this is the new label";
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", [newLabel]),
        };
        
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{customer}/collections/{RootCollection.Id}", collection.AsJson(), dbContext.GetETag(dbCollection));
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Can update label");

        var fromDb =
            await dbContext.Collections.SingleAsync(c =>
                c.Id == KnownCollections.RootCollection && c.CustomerId == customer);
        fromDb.Label.Values.Should().ContainSingle(newLabel);
    }
    
    [Fact]
    public async Task Put_CanMakePrivate()
    {
        const int customer = 1234891;
        var dbCollection = await dbContext.Collections.AddTestCollection(KnownCollections.RootCollection, customer, slug: "root", parent: null);
        await dbContext.SaveChangesAsync();
        
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["repository root"]),
        };
        
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{customer}/collections/{RootCollection.Id}", collection.AsJson(), dbContext.GetETag(dbCollection));
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Can update public/private");

        var fromDb =
            await dbContext.Collections.SingleAsync(c =>
                c.Id == KnownCollections.RootCollection && c.CustomerId == customer);
        fromDb.IsPublic.Should().BeFalse();
    }
    
    [Fact]
    public async Task Put_CanChangeThumbnail()
    {
        const int customer = 1234890;
        var dbCollection = await dbContext.Collections.AddTestCollection(KnownCollections.RootCollection, customer, slug: "root", parent: null);
        await dbContext.SaveChangesAsync();

        const string thumbnail = "https://path/test/image.jpg";
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["repository root"]),
            Thumbnail =
            [
                new Image
                {
                    Id = thumbnail,
                    Width = 100,
                    Height = 100,
                }
            ]
        };

        var asJson = collection.AsJson();
        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{customer}/collections/{RootCollection.Id}", asJson, dbContext.GetETag(dbCollection));
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Can update thumbnail");

        var fromDb =
            await dbContext.Collections.SingleAsync(c =>
                c.Id == KnownCollections.RootCollection && c.CustomerId == customer);
        fromDb.Thumbnail.Should().Be(thumbnail);
    }
}
