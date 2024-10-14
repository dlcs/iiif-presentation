using System.Net;
using API.Auth;
using API.Settings;
using LazyCache.Mocks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Stubbery;
using Test.Helpers.Settings;

namespace API.Tests.Auth;

public class DelegatedAuthenticatorTests : IClassFixture<ApiStub>
{
    private readonly DelegatedAuthenticator sut;

    public DelegatedAuthenticatorTests(ApiStub apiStub)
    {
        apiStub.EnsureStarted();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiStub.Address)
        };
        apiStub
            .Get("/customers/10", (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
            .IfHeader("Authorization", "Bearer 12345");
        
        apiStub
            .Get("/customers/10", (_, _) => new HttpResponseMessage(HttpStatusCode.OK))
            .IfHeader("Authorization", "Basic 12345");

        sut = new DelegatedAuthenticator(httpClient, OptionsHelpers.GetOptionsMonitor(new CacheSettings()),
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
        request.RouteValues.Add("customerId", "10");
        
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