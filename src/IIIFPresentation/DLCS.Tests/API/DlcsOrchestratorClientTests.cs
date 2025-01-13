using System.Net;
using DLCS.API;
using DLCS.Exceptions;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stubbery;

namespace DLCS.Tests.API;

public class DlcsOrchestratorClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RetrieveImagesForManifest_Throws_IfDownstreamNon200_NoReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 3;
        stub.Get($"/iiif-resource/{customerId}/batch-query/1,2", (_, _) => string.Empty).StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);
        
        Func<Task> action = () => sut.RetrieveImagesForManifest(customerId, [1, 2], CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>().WithMessage("Could not find a DlcsError in response");
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RetrieveImagesForManifest_Throws_IfDownstreamNon200_WithReturnedError(HttpStatusCode httpStatusCode)
    {
        using var stub = new ApiStub();
        const int customerId = 4;
        stub.Get($"/iiif-resource/{customerId}/batch-query/1,2", (_, _) => "{\"description\":\"I am broken\"}")
            .StatusCode((int)httpStatusCode);
        var sut = GetClient(stub);
        
        Func<Task> action = () => sut.RetrieveImagesForManifest(customerId, [1, 2], CancellationToken.None);
        await action.Should().ThrowAsync<DlcsException>().WithMessage("I am broken");
    }
    
    [Fact]
    public async Task RetrieveImagesForManifest_ReturnsManifest()
    {
        using var stub = new ApiStub();
        const int customerId = 5;
        stub.Get($"/iiif-resource/{customerId}/batch-query/1",
                (_, _) => "{\"id\":\"some/id\", \"type\": \"Manifest\" }")
            .StatusCode(200);
        var sut = GetClient(stub);
        var expected = new Manifest() { Id = "some/id" }; 
        
        var retrievedImages = await sut.RetrieveImagesForManifest(customerId, [1], CancellationToken.None);

        retrievedImages.Should().BeEquivalentTo(expected);
    }
    
    private static DlcsOrchestratorClient GetClient(ApiStub stub)
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
        
        return new DlcsOrchestratorClient(httpClient, options, new NullLogger<DlcsOrchestratorClient>());
    }
}
