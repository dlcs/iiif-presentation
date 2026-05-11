using System.Net;
using System.Text.Json;
using Core.Helpers;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.DLCS;
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
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
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
    public async Task IngestDeliverables_ReturnsListOfSingleBatch_IfIngested()
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
        var batches = await sut.IngestDeliverables(customerId, new List<JObject>() { jsonObject }, cancellationToken: CancellationToken.None);

        batches.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task IngestDeliverables_ReturnsListOfMultipleBatch_IfIngestedWithSplit()
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

        var batches = await sut.IngestDeliverables(customerId, new List<JObject> { jsonObject, secondJsonObject },
            cancellationToken: CancellationToken.None);

        batches.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task IngestDeliverables_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Post($"/customers/{customerId}/queue", (_, _) => "{\"description\":\"I am broken\"}")
            .IfBody(body => body.Contains("\"someString\""))
            .StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);

        dynamic jsonObject = new JObject();
        jsonObject.someObject = "someString";
        Func<Task> action = () => sut.IngestDeliverables(customerId, [jsonObject],
            cancellationToken: CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
    }

    [Fact]
    public async Task IngestDeliverables_PostsToAdjunctQueue_WhenAdjunctQueueTrue()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Post($"/customers/{customerId}/adjunctQueue",
                (_, _) => "{ \"@id\": \"customers/26/queue/batches/1234\" }")
            .IfBody(body => body.Contains("{\"someObject\":\"someValue\"}"))
            .StatusCode(201);
        var sut = GetClient(stub);
        var expected = new List<Batch> { new() { ResourceId = "customers/26/queue/batches/1234" } }; 
        
        dynamic jsonObject = new JObject();
        jsonObject.someObject = "someValue";
        var batches = await sut.IngestDeliverables(customerId, [jsonObject],
            adjunctQueue: true, cancellationToken: CancellationToken.None);

        batches.Should().BeEquivalentTo(expected);
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
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
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
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
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
            .Response((request, _) =>
            {
                var body = request.Body.ReadAsStringAsync().Result;
                var parsed = JsonSerializer.Deserialize<BulkPatchAssets>(body);

                var members = parsed!.Members.Select(m => new Asset
                        { Id = m.Id.GetLastPathElement(), Space = Int32.Parse(m.Id.Split('/').SkipLast(1).Last()) })
                    .ToArray();
                
                return JsonSerializer.Serialize(new HydraCollection<Asset>(members));
            }).StatusCode(200);
        var sut = GetClient(stub);

        var assets = await sut.UpdateAssetManifest(customerId, 
            [
                $"{customerId}/1/someString",
                $"{customerId}/1/someString2"
            ],
            OperationType.Add, ["first"], CancellationToken.None);

        assets.Should().HaveCount(2);
        assets.Should().Contain(x => x.Id == "someString");
        assets.Should().Contain(x => x.Id == "someString2");
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


    [Fact]
    public async Task DeleteAdjuncts_SerializesIdAsString_NotObject()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        var capturedBody = string.Empty;

        stub.Request(HttpMethod.Post).IfRoute($"/customers/{customerId}/deleteAdjuncts")
            .Response((request, _) =>
            {
                capturedBody = request.Body.ReadAsStringAsync().Result;
                return string.Empty;
            }).StatusCode(204);

        var sut = GetClient(stub);

        await sut.DeleteAdjuncts(customerId,
            [new AdjunctAssetIdentifier { Id = new AssetId(customerId, 1, "asset1"), Adjunct = ["a"] }],
            CancellationToken.None);

        capturedBody.Should().Contain($"\"{customerId}/1/asset1\"");
        capturedBody.Should().NotContain("\"customer\"");
    }

    [Fact]
    public async Task DeleteAdjuncts_MakesSingleRequest_WhenTotalAdjunctCountWithinLimit()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        var callCount = 0;

        stub.Request(HttpMethod.Post).IfRoute($"/customers/{customerId}/deleteAdjuncts")
            .Response((_, _) =>
            {
                callCount++;
                return string.Empty;
            }).StatusCode(204);

        var sut = GetClient(stub, maxImageListSize: 5);

        var adjuncts = new List<AdjunctAssetIdentifier>
        {
            new() { Id = new AssetId(customerId, 1, "asset1"), Adjunct = ["a", "b"] },
            new() { Id = new AssetId(customerId, 1, "asset2"), Adjunct = ["c"] }
        };

        await sut.DeleteAdjuncts(customerId, adjuncts, CancellationToken.None);

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAdjuncts_MakesMultipleRequests_WhenTotalAdjunctCountExceedsLimit()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        var callCount = 0;

        stub.Request(HttpMethod.Post).IfRoute($"/customers/{customerId}/deleteAdjuncts")
            .Response((_, _) =>
            {
                callCount++;
                return string.Empty;
            }).StatusCode(204);

        // Two items each with 3 adjuncts (total 6) exceeds limit of 5 → 2 requests
        var sut = GetClient(stub, maxImageListSize: 5);

        var adjuncts = new List<AdjunctAssetIdentifier>
        {
            new() { Id = new AssetId(customerId, 1, "asset1"), Adjunct = ["a", "b", "c"] },
            new() { Id = new AssetId(customerId, 1, "asset2"), Adjunct = ["d", "e", "f"] }
        };

        await sut.DeleteAdjuncts(customerId, adjuncts, CancellationToken.None);

        callCount.Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task DeleteAdjuncts_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Request(HttpMethod.Post).IfRoute($"/customers/{customerId}/deleteAdjuncts")
            .Response((_, _) => "{\"description\":\"I am broken\"}")
            .StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);

        Func<Task> action = () => sut.DeleteAdjuncts(customerId,
            [new AdjunctAssetIdentifier { Id = new AssetId(customerId, 1, "asset1"), Adjunct = ["a"] }],
            CancellationToken.None);

        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
    }

    [Fact]
    public async Task DeleteAdjuncts_SplitsItem_WhenSingleItemExceedsLimit()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        var callCount = 0;

        stub.Request(HttpMethod.Post).IfRoute($"/customers/{customerId}/deleteAdjuncts")
            .Response((_, _) =>
            {
                callCount++;
                return string.Empty;
            }).StatusCode(204);

        // 4 adjuncts with a limit of 3 → split into [a,b,c] and [d] → 2 requests
        var sut = GetClient(stub, maxImageListSize: 3);

        var adjuncts = new List<AdjunctAssetIdentifier>
        {
            new() { Id = new AssetId(customerId, 1, "asset1"), Adjunct = ["a", "b", "c", "d"] }
        };

        await sut.DeleteAdjuncts(customerId, adjuncts, CancellationToken.None);

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task GetCustomerImages_IncludesAdjunctsQueryParam_WhenCalledByAssetIds()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        var capturedQuery = string.Empty;

        stub.Post($"/customers/{customerId}/allImages",
                (req, _) =>
                {
                    capturedQuery = req.QueryString.Value ?? string.Empty;
                    return """{ "@id": "customers/5/allImages", "member": [] }""";
                })
            .StatusCode(200);

        var sut = GetClient(stub);
        await sut.GetCustomerImages(customerId, ["someString"], CancellationToken.None);

        capturedQuery.Should().Contain("include=adjuncts");
    }

    [Fact]
    public async Task GetCustomerImages_IncludesAdjunctsQueryParam_WhenCalledByManifestId()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        var capturedQuery = string.Empty;

        stub.Get($"/customers/{customerId}/allImages",
                (req, _) =>
                {
                    capturedQuery = req.QueryString.Value ?? string.Empty;
                    return """{ "@id": "customers/5/allImages", "member": [] }""";
                })
            .StatusCode(200);

        var sut = GetClient(stub);
        await sut.GetCustomerImages(customerId, "someManifest", CancellationToken.None);

        capturedQuery.Should().Contain("include=adjuncts");
    }

    private static DlcsApiClient GetClient(ApiStub stub, int maxBatchSize = 1, int maxImageListSize = 500)
    {
        stub.EnsureStarted();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(stub.Address)
        };

        var options = Options.Create(new DlcsSettings()
        {
            ApiUri = new Uri("https://localhost"),
            MaxBatchSize = maxBatchSize,
            MaxImageListSize = maxImageListSize
        });

        return new DlcsApiClient(httpClient, options, new NullLogger<DlcsApiClient>());
    }
}
