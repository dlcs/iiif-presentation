using System.Net;
using DLCS.API;
using DLCS.Exceptions;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Options;
using Stubbery;

namespace DLCS.Tests.API;

public class DlcsOrchestratorClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RetrieveAssetsForManifest_Throws_IfDownstreamNon200_NoReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 3;
        stub.Get(
            $"/iiif-resource/v3/{customerId}/batch-query/1,2",
            (_, _) => string.Empty).StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);
        
        Func<Task> action = () => sut.RetrieveAssetsForManifest(customerId, [1, 2], CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "Could not find a DlcsError in response" && e.StatusCode == httpStatusCode);
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RetrieveAssetsForManifest_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Get($"/iiif-resource/v3/{customerId}/batch-query/1,2", (_, _) => "{\"description\":\"I am broken\"}")
            .StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);
        
        Func<Task> action = () => sut.RetrieveAssetsForManifest(customerId, [1, 2], CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>()
            .Where(e => e.Message == "I am broken" && e.StatusCode == httpStatusCode);
    }
    
    [Fact]
    public async Task RetrieveAssetsForManifest_ReturnsManifest()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Get($"/iiif-resource/v3/{customerId}/batch-query/1",
                (_, _) => "{\"id\":\"some/id\", \"type\": \"Manifest\" }")
            .StatusCode(200);
        var sut = GetClient(stub);
        var expected = new Manifest() { Id = "some/id" }; 
        
        var retrievedManifest = await sut.RetrieveAssetsForManifest(customerId, [1], CancellationToken.None);

        retrievedManifest.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task RetrieveAssetsForManifest_ReturnsManifest_FromCustomerSpecificSettings()
    {
        using var stub = new ApiStub();
        const int customerId = -5;
        stub.Get($"/iiif-resource/v3/{customerId}/batch-query/1",
                (_, _) => "{\"id\":\"some/id\", \"type\": \"Manifest\" }")
            .StatusCode(200);
        var sut = GetClient(stub, settings =>
        {
            settings.OrchestratorUri = new Uri("http://0.0.0.0"); // invalid address, ensures not used
            settings.CustomerOrchestratorUri[customerId] = new Uri(stub.Address);
        });
        var expected = new Manifest() { Id = "some/id" }; 
        
        var retrievedManifest = await sut.RetrieveAssetsForManifest(customerId, [1], CancellationToken.None);

        retrievedManifest.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task RetrieveAssetsForManifest_PassesCacheBustParam()
    {
        using var stub = new ApiStub();
        const int customerId = 6;
        string? passedQueryParam = null;
        stub.Get($"/iiif-resource/v3/{customerId}/batch-query/1",
                (req, _) =>
                {
                    passedQueryParam = req.Query["cacheBust"];
                    return "{\"id\":\"some/id\", \"type\": \"Manifest\" }";
                })
            .StatusCode(200);
        var sut = GetClient(stub);
        
        await sut.RetrieveAssetsForManifest(customerId, [1], CancellationToken.None);

        passedQueryParam.Should().NotBeNull("?cacheBust query param passed to avoid caching issues");
    }
    
    private static DlcsOrchestratorClient GetClient(ApiStub stub, Action<DlcsSettings>? configureSettings = null)
    {
        stub.EnsureStarted();

        var httpClient = new HttpClient();

        var dlcsSettings = new DlcsSettings
        {
            ApiUri = new Uri("https://localhost"),
            OrchestratorUri = new Uri(stub.Address),
        };
        configureSettings?.Invoke(dlcsSettings);
        
        return new DlcsOrchestratorClient(httpClient, Options.Create(dlcsSettings));
    }
}
