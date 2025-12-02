using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Infrastructure;
using Core.Response;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Models.API.Collection;
using Models.API.Manifest;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

// We want to prep a unique grandparent->parent path for all the subsequent tests
public class RestPathFixture : PresentationAppFactory<Program>
{
    public readonly HttpClient httpClient;
    private const int Customer = RestPathBehaviourTests.Customer;

    public RestPathFixture(StorageFixture storageFixture)
    {
        httpClient = this.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        SetupTargetCollection();
    }

    private void SetupTargetCollection()
    {
        // 1. Grandparent
        var collection = new PresentationCollection
        {
            Behavior =
            [
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            ],
            Label = new LanguageMap("en", ["the grandparent collection"]),
            Slug = ManifestPathTestProvider.Grandparent,
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Thumbnail = [new Image { Id = "some/thumbnail" }],
            Tags = "some, tags",
            ItemsOrder = 1,
        };

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/collections",
            collection.AsJson());

        // Act
        var response = httpClient.AsCustomer().SendAsync(requestMessage).Result;
        if (!response.IsSuccessStatusCode)
            throw new("Can't create grandparent collection");

        var responseCollection = response.ReadAsPresentationResponseAsync<PresentationCollection>().Result;
        var grandparentId = responseCollection!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();

        // 2. Parent
        collection = new PresentationCollection
        {
            Behavior = new List<string>
            {
                Behavior.IsPublic,
                Behavior.IsStorageCollection
            },
            Label = new LanguageMap("en", ["test collection"]),
            Slug = ManifestPathTestProvider.Parent,
            Parent = $"http://localhost/{Customer}/collections/{grandparentId}",
            Thumbnail = [new Image { Id = "some/thumbnail" }],
            Tags = "some, tags",
            ItemsOrder = 1,
        };
        requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/collections/{ManifestPathTestProvider.ParentId}",
            collection.AsJson());

        // Act
        response = httpClient.AsCustomer().SendAsync(requestMessage).Result;
        if (!response.IsSuccessStatusCode)
            throw new("Can't create parent collection");
    }
}

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.RestCollection.CollectionName)]
public class RestPathBehaviourTests : IClassFixture<RestPathFixture>
{
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 amazonS3;
    private readonly HttpClient httpClient;

    public const int Customer = 1;

    public RestPathBehaviourTests(StorageFixture storageFixture, RestPathFixture fixture)
    {
        dbContext = storageFixture.DbFixture.DbContext;

        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        httpClient = fixture.httpClient;
        // parent = dbContext.Collections
        //     .First(x => x.CustomerId == Customer && x.Hierarchy!.Any(h => h.Slug == string.Empty)).Id;

        storageFixture.DbFixture.CleanUp();
    }

    [Theory]
    [ClassData(typeof(ManifestPathTestProvider))]
    public async Task TestManifestPaths(string method, string url, string? id, string? parent, string? slug, string? publicId)
    {
        // passing as string because XUnit asked me to - parsing back to typed obj
        var httpMethod = HttpMethod.Parse(method);

        // Arrange
        var manifest = new PresentationManifest();
        if (parent != null)
            manifest.Parent = $"http://localhost/{Customer}/{parent}";

        if (slug != null)
            manifest.Slug = slug;
        if (id != null)
            manifest.Id = $"http://localhost/{Customer}/{id}";
        if (publicId != null)
            manifest.PublicId = $"http://localhost/{Customer}/{publicId}";

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(httpMethod,
            $"{Customer}/{url}",
            manifest.AsJson());
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var manifestId = responseManifest!.Id!.Split('/', StringSplitOptions.TrimEntries).Last();
        try
        {
            manifestId.Should().Be(ManifestPathTestProvider.Id);
            responseManifest.Slug.Should().Be(ManifestPathTestProvider.Slug);
        }
        finally
        {
            // clean up
            requestMessage =
                HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, $"{Customer}/manifests/{manifestId}");
            _ = await httpClient.AsCustomer().SendAsync(requestMessage);
        }
    }
    
    [Theory]
    [ClassData(typeof(CollectionPathTestProvider))]
    public async Task TestCreateCollectionPaths(string method, string url, string? id, string? parent, string? slug, string? publicId, string expectedSlug)
    {
        
        // passing as string because XUnit asked me to - parsing back to typed obj
        var httpMethod = HttpMethod.Parse(method);

        // Arrange
        var collection = new PresentationCollection();
        if (parent != null)
            collection.Parent = $"http://localhost/{Customer}/{parent}";
        if (slug != null)
            collection.Slug = slug;
        if (id != null)
            collection.Id = $"http://localhost/{Customer}/{id}";
        if (publicId != null)
            collection.PublicId = $"http://localhost/{Customer}/{publicId}";

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(httpMethod,
            $"{Customer}/{url}",
            collection.AsJson());
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationCollection>();
        responseCollection!.Slug.Should().Be(expectedSlug);
    }
}

public class CollectionPathTestProvider : TheoryData<string, string, string?, string?, string?, string?, string>
{
    public const string Collections = "collections/";

    public const string Id = "example-abc";
    public const string Slug = "my-slug";
    public const string Parent = "parent";
    public const string ParentId = "foobar";
    public const string Grandparent = "grandparent";

    private int _slugCounter = 1;
    private string UniqueSlug() => $"{Slug}-{_slugCounter++}";

    public CollectionPathTestProvider()
    {
        // ( method,  url,  id,  parent,  slug,  publicId)
        
        // POST to flat collections endpoint, use FLAT parent
        var slug = UniqueSlug();
        Add(HttpMethod.Post.ToString(),Collections,null,Collections+ParentId, slug,null, slug);
        
        
        // POST to flat collections endpoint, use HIERARCHICAL parent
        slug = UniqueSlug();
        Add(HttpMethod.Post.ToString(),Collections,null,$"{Grandparent}/{Parent}",slug,null, slug);

        // PUT to the desired PUBLIC hierarchical path
        slug = UniqueSlug();
        Add(HttpMethod.Put.ToString(), $"{Grandparent}/{Parent}/{slug}", null, null, null, null, slug);
        
        // POST into the parent public hierarchical collection
        slug = UniqueSlug();
        Add(HttpMethod.Post.ToString(),$"{Grandparent}/{Parent}", null, null, slug,null, slug);
        
        // PUT to the FLAT API Collections endpoint (2 variants, hier/flat parent)
        slug = UniqueSlug();
        Add(HttpMethod.Put.ToString(), Collections + Id + "-1", null, $"{Grandparent}/{Parent}", slug, null, slug);
        slug = UniqueSlug();
        Add(HttpMethod.Put.ToString(), Collections + Id + "-2", null, Collections+ParentId, slug, null, slug);
        
        // POST Vanilla IIIF into the parent public hierarchical collection
        slug = UniqueSlug();
        Add(HttpMethod.Post.ToString(), $"{Grandparent}/{Parent}", $"{Grandparent}/{Parent}/{slug}", null, null, null, slug);
        
        // PUT Vanilla IIIF to the public hierarchical URL
        slug = UniqueSlug();
        Add(HttpMethod.Put.ToString(), $"{Grandparent}/{Parent}/{slug}", $"{Grandparent}/{Parent}/{slug}", null, null,
            null, slug);
    }
}

public class ManifestPathTestProvider : TheoryData<string, string, string?, string?, string?, string?>
{
    private const string Collections = "collections/";
    private const string Manifests = "manifests/";

    public const string Id = "example-abc";
    public const string Slug = "my-slug";
    public const string Parent = "parent";
    public const string ParentId = "foobar";
    public const string Grandparent = "grandparent";
    
    public ManifestPathTestProvider()
    {
        // ( method,  url,  id,  parent,  slug,  publicId)

        // API PUT
        Add(HttpMethod.Put.ToString(), Manifests + Id, null, null, null, $"{Grandparent}/{Parent}/{Slug}");
        Add(HttpMethod.Put.ToString(), Manifests + Id, null, Collections + ParentId, Slug, null);
        Add(HttpMethod.Put.ToString(), Manifests + Id, null, $"{Grandparent}/{Parent}", Slug, null);
        Add(HttpMethod.Put.ToString(), Manifests + Id, Manifests + Id, Collections + ParentId, Slug,
            $"/{Grandparent}/{Parent}/{Slug}");
        Add(HttpMethod.Put.ToString(), Manifests + Id, Manifests + Id, $"{Grandparent}/{Parent}", Slug,
            $"/{Grandparent}/{Parent}/{Slug}");

        // API POST
        Add(HttpMethod.Post.ToString(), Manifests, Manifests + Id, null, null,
            $"{Grandparent}/{Parent}/{Slug}");
        Add(HttpMethod.Post.ToString(), Manifests, Manifests + Id, Collections + ParentId, Slug, null);
        Add(HttpMethod.Post.ToString(), Manifests, Manifests + Id, $"{Grandparent}/{Parent}", Slug, null);
        Add(HttpMethod.Post.ToString(), Manifests, Manifests + Id, Collections + ParentId, Slug,
            $"/{Grandparent}/{Parent}/{Slug}");
        Add(HttpMethod.Post.ToString(), Manifests, Manifests + Id, $"{Grandparent}/{Parent}", Slug,
            $"/{Grandparent}/{Parent}/{Slug}");

        // Hierarchical POST
        Add(HttpMethod.Post.ToString(), $"{Grandparent}/{Parent}", Manifests + Id, null, null, null);
        Add(HttpMethod.Post.ToString(), $"{Grandparent}/{Parent}", Manifests + Id, null, null,
            $"/{Grandparent}/{Parent}/{Slug}");
        Add(HttpMethod.Post.ToString(), $"{Grandparent}/{Parent}", Manifests + Id, Collections + ParentId,
            Slug,
            null);
        Add(HttpMethod.Post.ToString(), $"{Grandparent}/{Parent}", Manifests + Id,
            $"{Grandparent}/{Parent}",
            Slug, null);
        Add(HttpMethod.Post.ToString(), $"{Grandparent}/{Parent}", Manifests + Id, Collections + ParentId,
            Slug,
            $"/{Grandparent}/{Parent}/{Slug}");
        Add(HttpMethod.Post.ToString(), $"{Grandparent}/{Parent}", Manifests + Id,
            $"{Grandparent}/{Parent}",
            Slug, $"/{Grandparent}/{Parent}");
    }
}
