using API.Auth;
using API.Settings;
using DLCS.API;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;

namespace API.Tests.Auth;

public class DelegatedAuthenticatorTests
{
    private readonly DelegatedAuthenticator sut;

    public DelegatedAuthenticatorTests()
    {
        var fakeClient = A.Fake<IDlcsApiClient>();
        A.CallTo(() => fakeClient.IsRequestAuthenticated(10, CancellationToken.None)).Returns(true);

        sut = new DelegatedAuthenticator(fakeClient, OptionsHelpers.GetOptionsMonitor(new CacheSettings()),
            new MockCachingService(), new NullLogger<DelegatedAuthenticator>());
    }
    
    [Fact]
    public async Task ValidateRequest_NoCredentials_IfNoAuthHeader()
    {
        var request = new DefaultHttpContext().Request;
        
        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.NoCredentials);
    }
    
    [Fact]
    public async Task ValidateRequest_NoCredentials_IfAuthHeaderEmpty()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = "";
        
        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.NoCredentials);
    }
    
    [Fact]
    public async Task ValidateRequest_NoCredentials_IfNoCustomerIdInRoute()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = "Basic 12345";
        request.RouteValues.Add("NotCustomerId", "123");
        
        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.NoCredentials);
    }
    
    [Theory]
    [InlineData("Basic 9999")]
    [InlineData("Bearer 9999")]
    public async Task ValidateRequest_Fail_IfDownstreamNon200(string authHeader)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = authHeader;
        request.RouteValues.Add("customerId", "11");
        
        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.Failed);
    }
    
    [Theory]
    [InlineData("Basic 12345")]
    [InlineData("Bearer 12345")]
    public async Task ValidateRequest_Pass_IfDownstream200(string authHeader)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = authHeader;
        request.RouteValues.Add("customerId", "10");
        
        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.Success);
    }
}