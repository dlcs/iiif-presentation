using System.Net;
using System.Text.Json;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Stubbery;

namespace DLCS.Tests.API;

public class DlcsApiClientTests
{
    [Fact]
    public async Task IsRequestAuthenticated_True_IfDownstream200()
    {
        using var stub = new ApiStub();
        const int customerId = 1;
        stub.Get($"/customers/{customerId}", (_, _) => string.Empty).StatusCode(200);
        var sut = GetClient(stub);
        var result = await sut.IsRequestAuthenticated(customerId);
        result.Should().BeTrue();
    }
    
    [Fact]
    public async Task IsRequestAuthenticated_False_IfDownstreamNon200()
    {
        using var stub = new ApiStub();
        const int customerId = 2;
        stub.Get($"/customers/{customerId}", (_, _) => string.Empty).StatusCode(502);
        var sut = GetClient(stub);
        
        var result = await sut.IsRequestAuthenticated(customerId);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task CreateSpace_Throws_IfDownstreamNon200_NoReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 3;
        stub.Post($"/customers/{customerId}/spaces", (_, _) => string.Empty).StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);
        
        Func<Task> action = () => sut.CreateSpace(customerId, "hi", CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "Could not find a DlcsError in response" && e.StatusCode == httpStatusCode);
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task CreateSpace_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Post($"/customers/{customerId}/spaces", (_, _) => "{\"description\":\"I am broken\"}")
            .IfBody(body => body == "{\"name\":\"hi\"}")
            .StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);
        
        Func<Task> action = () => sut.CreateSpace(customerId, "hi", CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);;
    }
    
    [Fact]
    public async Task CreateSpace_ReturnsSpace_IfCreated()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Post($"/customers/{customerId}/spaces",
                (_, _) => "{\"id\":\"1234\", \"name\": \"eden\", \"@id\": \"https://local/customers/5/spaces/1234\" }")
            .IfBody(body => body == "{\"name\":\"eden\"}")
            .StatusCode(201);
        var sut = GetClient(stub);
        var expected = new Space { Id = 1234, Name = "eden", ResourceId = "https://local/customers/5/spaces/1234" }; 
        
        var createdSpace = await sut.CreateSpace(customerId, "eden", CancellationToken.None);

        createdSpace.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task IngestAssets_ReturnsListOfSingleBatch_IfIngested()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Post($"/customers/{customerId}/queue",
                (_, _) => "{ \"@id\": \"customers/26/queue/batches/1234\" }")
            .IfBody(body => body.Contains("{\"someObject\":\"someValue\"}"))
            .StatusCode(201);
        var sut = GetClient(stub);
        var expected = new List<Batch> { new() { ResourceId = "customers/26/queue/batches/1234" } }; 
        
        dynamic jsonObject = new JObject();
        jsonObject.someObject = "someValue";
        var batches = await sut.IngestAssets(customerId, new List<JObject>() { jsonObject }, CancellationToken.None);

        batches.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task IngestAssets_ReturnsListOfMultipleBatch_IfIngestedWithSplit()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Post($"/customers/{customerId}/queue",
                (_, _) => "{ \"@id\": \"customers/26/queue/batches/1234\" }")
            .IfBody(body => body.Contains("{\"someObject\":\"someValue\"}"))
            .StatusCode(201);
        var sut = GetClient(stub);
        var expected = new List<Batch>
        {
            new() { ResourceId = "customers/26/queue/batches/1234" }, 
            new() { ResourceId = "customers/26/queue/batches/1234" }
        }; 
        
        dynamic jsonObject = new JObject();
        jsonObject.someObject = "someValue";
        
        dynamic secondJsonObject = new JObject();
        secondJsonObject.someObject = "someValue";

        var batches = await sut.IngestAssets(customerId, new List<JObject> { jsonObject, secondJsonObject },
            CancellationToken.None);

        batches.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task IngestAssets_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Post($"/customers/{customerId}/queue", (_, _) => "{\"description\":\"I am broken\"}")
            .IfBody(body => body.Contains("\"someString\""))
            .StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);
        
        Func<Task> action = () => sut.IngestAssets(customerId, new List<string> {"someString"}, CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
    }

    [Fact]
    public async Task GetBatchAssets_ReturnsListOfAssets_WhenAssets()
    {
        const int batchId = 2137;

        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Get($"/customers/{customerId}/queue/batches/{batchId}/assets",
                (_, _) => """
                          {
                           "@id": "customers/5/queue/batches/2137/assets",
                           "member": [
                            { "someAssetProp": "someAssetValue-this can be arbitrary" }
                           ]
                           }
                          """)
            .StatusCode(201);
        var sut = GetClient(stub);

        var assets = await sut.GetBatchAssets(customerId, batchId, CancellationToken.None);

        assets.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBatchAssets_ReturnsListOfAssets_WhenNoAssets()
    {
        const int batchId = 2137;

        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Get($"/customers/{customerId}/queue/batches/{batchId}/assets",
                (_, _) => """
                          {
                           "@id": "customers/5/queue/batches/2137/assets",
                            "fnord": "I have no member prop even"
                           }
                          """)
            .StatusCode(201);
        var sut = GetClient(stub);

        var assets = await sut.GetBatchAssets(customerId, batchId, CancellationToken.None);

        assets.Should().NotBeNull().And.BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task GetBatchAssets_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        const int batchId = 2137;
        stub.Get($"/customers/{customerId}/queue/batches/{batchId}/assets",
                (_, _) => "{\"description\":\"I am broken\"}")
            .StatusCode((int) httpStatusCode);
        var sut = GetClient(stub);

        Func<Task> action = () => sut.GetBatchAssets(customerId, batchId, CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
    }

    [Fact]
    public async Task GetCustomerImages_ReturnsListOfAssets_WhenAssets()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Post($"/customers/{customerId}/allImages",
                (_, _) => """
                          {
                           "@id": "customers/5/queue/batches/2137/assets",
                           "member": [
                            { "someAssetProp": "someAssetValue-this can be arbitrary" }
                           ]
                           }
                          """)
            .IfBody(body => body.Contains("\"someString\""))
            .StatusCode(201);
        var sut = GetClient(stub);

        var assets = await sut.GetCustomerImages(customerId, ["someString"], CancellationToken.None);

        assets.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCustomerImages_ReturnsListOfAssets_WhenNoAssets()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Post($"/customers/{customerId}/allImages",
                (_, _) => """
                          {
                           "@id": "customers/5/queue/batches/2137/assets",
                            "fnord": "I have no member prop even"
                           }
                          """)
            .IfBody(body => body.Contains("\"someString\""))
            .StatusCode(201);
        var sut = GetClient(stub);

        var assets = await sut.GetCustomerImages(customerId, ["someString"], CancellationToken.None);

        assets.Should().NotBeNull().And.BeEmpty();
    }
    
    [Fact]
    public async Task GetCustomerImages_StripsDuplicateAssets_WhenDuplicateAssetIds()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Post($"/customers/{customerId}/allImages",
                (_, _) => """
                          {
                           "@id": "customers/5/queue/batches/2137/assets",
                           "member": [
                            { "someAssetProp": "someAssetValue-this can be arbitrary" }
                           ]
                           }
                          """)
            .IfBody(body => body.Contains("\"member\":[{\"id\":\"someString\"}]"))
            .StatusCode(201);
        var sut = GetClient(stub);

        var assets = await sut.GetCustomerImages(customerId, ["someString", "someString"], CancellationToken.None);

        assets.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task GetCustomerImages_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Post($"/customers/{customerId}/allImages",
                (_, _) => "{\"description\":\"I am broken\"}")
            .IfBody(body => body.Contains("\"someString\""))
            .StatusCode((int) httpStatusCode);
        var sut = GetClient(stub);

        Func<Task> action = () => sut.GetCustomerImages(customerId, ["someString"], CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);;
    }
    
    [Fact]
    public async Task GetCustomerImagesManifest_ReturnsListOfAssets_WhenNoAssets()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Get($"/customers/{customerId}/allImages",
                (_, _) => """
                          {
                           "@id": "customers/5/queue/batches/2137/assets",
                            "fnord": "I have no member prop even"
                           }
                          """)
            .StatusCode(201);
        var sut = GetClient(stub);

        var assets = await sut.GetCustomerImages(customerId, "someManifest", CancellationToken.None);

        assets.Should().NotBeNull().And.BeEmpty();
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GetCustomerImagesManifest__ReturnsCorrectNumberOfAssets_WhenCalledRepeatedly(int manifestCalls)
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        var manifestId = "someManifest";
        
        stub.Get($"/customers/{customerId}/allImages", (_, args) =>
            {
                var page = Convert.ToInt32(args.Query.page);
                
                return $@"
                          {{
                               ""$@id"": ""customers/5/queue/batches/2137/assets"",
                               ""member"": [
                                {{ ""someAssetProp"": ""someAssetValue-this can be arbitrary"" }}
                               ],
                                ""view"": {{
                                    {(page < manifestCalls ? $"\"next\" : \"https://localhost/customers/{customerId}/allImages?page={++page}\"" : "")}
                                }}
                           }}
                          ";
            })
            .StatusCode(201);
        
        var sut = GetClient(stub);

        var assets = await sut.GetCustomerImages(customerId, manifestId, CancellationToken.None);

        assets.Should().HaveCount(manifestCalls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task GetCustomerImagesManifest_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Get($"/customers/{customerId}/allImages",
                (_, _) => "{\"description\":\"I am broken\"}")
            .StatusCode((int) httpStatusCode);
        var sut = GetClient(stub);

        Func<Task> action = () => sut.GetCustomerImages(customerId, "someManifest", CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);;
    }
    
    [Fact]
    public async Task UpdateAssetWithManifest_ReturnsAssets_WhenSuccess()
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Request(HttpMethod.Patch).IfRoute($"/customers/{customerId}/allImages")
            .Response((_, _) => """
                                {
                                 "@type": "Collection",
                                 "totalItems": 1,
                                 "pageSize": 1,
                                 "member": [
                                  { "id": "someAssetId" }
                                 ]
                                 }
                                """).StatusCode(200);
        var sut = GetClient(stub);

        var assets = await sut.UpdateAssetManifest(customerId, [$"{customerId}/1/someString"],
            OperationType.Add, ["first"], CancellationToken.None);

        assets.Should().HaveCount(1);
        assets.Single().Id.Should().Be("someAssetId");
    }
    
    [Fact]
    public async Task UpdateAssetWithManifest_ReturnsMultipleAssets_WhenMultipleSuccess()
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Request(HttpMethod.Patch).IfRoute($"/customers/{customerId}/allImages")
            .Response((_, _) => """
                                {
                                 "@type": "Collection",
                                 "totalItems": 1,
                                 "pageSize": 1,
                                 "member": [
                                  { "id": "someAssetId" }
                                 ]
                                 }
                                """).StatusCode(200);
        var sut = GetClient(stub);

        var assets = await sut.UpdateAssetManifest(customerId, 
            [
                $"{customerId}/1/someString",
                $"{customerId}/1/someString2"
            ],
            OperationType.Add, ["first"], CancellationToken.None);

        assets.Should().HaveCount(2);
        assets.First().Id.Should().Be("someAssetId");
        assets.Last().Id.Should().Be("someAssetId");
    }
    
    [Fact]
    public async Task UpdateAssetWithManifest_ThrowsError_WhenAssetsReturnedDiffersFromAssetsAsked()
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Request(HttpMethod.Patch).IfRoute($"/customers/{customerId}/allImages")
            .Response((_, _) => """
                                {
                                 "@type": "Collection",
                                 "totalItems": 1,
                                 "pageSize": 1,
                                 "member": [
                                  { "id": "someString", "space": 1 },
                                  { "id": "someAssetId2", "space": 1 }
                                 ]
                                 }
                                """).StatusCode(200);
        var sut = GetClient(stub);

        Func<Task> action = () => sut.UpdateAssetManifest(customerId, 
            [
                $"{customerId}/1/someString",
                $"{customerId}/1/someString2"
            ],
            OperationType.Add, ["first"], CancellationToken.None);

        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "Could not find assets [4/1/someString2] in DLCS" &&
                        e.StatusCode == HttpStatusCode.InternalServerError);
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task UpdateAssetWithManifest_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Request(HttpMethod.Patch).IfRoute($"/customers/{customerId}/allImages")
            .Response((_, _) => "{\"description\":\"I am broken\"}")
            .StatusCode((int) httpStatusCode);
        var sut = GetClient(stub);

        Func<Task> action = () => sut.UpdateAssetManifest(customerId, [$"{customerId}/1/someString"],
            OperationType.Add, ["first"], CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
    }
    
    [Fact]
    public async Task UpdateAssetWithManifest_ReturnsDistinctAssets_WhenMultipleOfSameAsset()
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Request(HttpMethod.Patch).IfRoute($"/customers/{customerId}/allImages")
            .IfBody(body =>
            {
                var convertedBody = JsonSerializer.Deserialize<BulkPatchAssets>(body);

                if (convertedBody!.Members.GroupBy(m => m.Id).Any(g => g.Count() > 1))
                {
                    return true;
                }
                
                return false;
            })
            .Response((_, _) => JsonSerializer.Serialize(new DlcsError
            {
                Description = "duplicate assets found"
            })).StatusCode(400);
        
        stub.Request(HttpMethod.Patch).IfRoute($"/customers/{customerId}/allImages")
            .Response((_, _) => """
                                {
                                 "@type": "Collection",
                                 "totalItems": 1,
                                 "pageSize": 1,
                                 "member": [
                                  { "id": "someString", "space": 1 }
                                 ]
                                 }
                                """).StatusCode(200);
        var sut = GetClient(stub, 2);

        var assets = await sut.UpdateAssetManifest(customerId, 
            [
                $"{customerId}/1/someString",
                $"{customerId}/1/someString"
            ],
            OperationType.Add, ["first"], CancellationToken.None);

        assets.Should().HaveCount(1);
        assets.First().Id.Should().Be("someString");
    }


    private static DlcsApiClient GetClient(ApiStub stub, int maxBatchSize = 1)
    {
        stub.EnsureStarted();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(stub.Address)
        };

        var options = Options.Create(new DlcsSettings()
        {
            ApiUri = new Uri("https://localhost"),
            MaxBatchSize = maxBatchSize
        });

        return new DlcsApiClient(httpClient, options, new NullLogger<DlcsApiClient>());
    }
}
