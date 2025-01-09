using System.Net;
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
        await action.Should().ThrowAsync<DlcsException>().WithMessage("Could not find a DlcsError in response");
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
        await action.Should().ThrowAsync<DlcsException>().WithMessage("I am broken");
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
        await action.Should().ThrowAsync<DlcsException>().WithMessage("I am broken");
    }

    private static DlcsApiClient GetClient(ApiStub stub)
    {
        stub.EnsureStarted();
        
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(stub.Address)
        };

        var options = Options.Create(new DlcsSettings()
        {
            ApiUri = new Uri("https://localhost"),
            MaxBatchSize = 1
        });
        
        return new DlcsApiClient(httpClient, options, new NullLogger<DlcsApiClient>());
    }
}
