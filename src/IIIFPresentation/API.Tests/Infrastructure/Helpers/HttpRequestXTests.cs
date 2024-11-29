using API.Infrastructure.Helpers;
using Microsoft.AspNetCore.Http;

namespace API.Tests.Infrastructure.Helpers;

public class HttpRequestXTests
{
    [Fact]
    public void HasShowExtraHeader_False_IfNotFound()
    {
        var httpRequest = new DefaultHttpContext().Request;

        httpRequest.HasShowExtraHeader().Should().BeFalse();
    }
    
    [Fact]
    public void HasShowExtraHeader_False_IfHeaderPresent_ButUnknownValue()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Append("X-IIIF-CS-Show-Extras", "Foo");

        httpRequest.HasShowExtraHeader().Should().BeFalse();
    }
    
    [Theory]
    [InlineData("X-IIIF-CS-Show-Extras")]
    [InlineData("x-iiif-cs-show-extras")]
    public void HasShowExtraHeader_True_IfHeaderPresent_AndAllValue(string header)
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Append(header, "All");

        httpRequest.HasShowExtraHeader().Should().BeTrue();
    }

    [Fact]
    public void HasCreateSpaceHeader_False_IfNoLocationHeader()
    {
        var httpRequest = new DefaultHttpContext().Request;

        httpRequest.HasCreateSpaceHeader().Should().BeFalse();
    }
    
    [Fact]
    public void HasCreateSpaceHeader_False_IfHeaderPresent_ButUnknownValue()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Location = "Foo";

        httpRequest.HasCreateSpaceHeader().Should().BeFalse();
    }
    
    [Fact]
    public void HasCreateSpaceHeader_True_IfHeaderPresent_AndValueIsCorrectCase()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Location = "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"";

        httpRequest.HasCreateSpaceHeader().Should().BeTrue();
    }
    
    [Fact]
    public void HasCreateSpaceHeader_False_IfHeaderPresent_AndValueCorrectButWrongCase()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Location = "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"".ToLower();

        httpRequest.HasCreateSpaceHeader().Should().BeFalse();
    }
}