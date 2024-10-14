using API.Infrastructure.Helpers;
using Microsoft.AspNetCore.Http;

namespace API.Tests.Infrastructure.Helpers;

public class HttpRequestXTests
{
    [Fact]
    public void HttpRequestX_False_IfNotFound()
    {
        var httpRequest = new DefaultHttpContext().Request;

        httpRequest.HasShowExtraHeader().Should().BeFalse();
    }
    
    [Fact]
    public void HttpRequestX_False_IfHeaderPresent_ButUnknownValue()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Append("X-IIIF-CS-Show-Extras", "Foo");

        httpRequest.HasShowExtraHeader().Should().BeFalse();
    }
    
    [Theory]
    [InlineData("X-IIIF-CS-Show-Extras")]
    [InlineData("x-iiif-cs-show-extras")]
    public void HttpRequestX_False_IfHeaderPresent_AndAllValue(string header)
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Append(header, "All");

        httpRequest.HasShowExtraHeader().Should().BeTrue();
    }
    
    [Fact]
    public void HttpRequestX_False_IfHeaderPresent_AndAllValue_Case()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Append("X-IIIF-CS-Show-Extras", "All");

        httpRequest.HasShowExtraHeader().Should().BeTrue();
    }
}