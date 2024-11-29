using DLCS.API;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static DlcsApiClient GetClient(ApiStub stub)
    {
        stub.EnsureStarted();
        
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(stub.Address)
        };
        
        return new DlcsApiClient(httpClient, new NullLogger<DlcsApiClient>());
    }
}