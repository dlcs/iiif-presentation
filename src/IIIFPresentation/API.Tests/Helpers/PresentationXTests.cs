using API.Helpers;
using Models.API.Collection;

namespace API.Tests.Helpers;

public class PresentationXTests
{
    [Fact]
    public void PublicIdIsRoot_True()
    {
        const string baseUrl = "https://test.example";
        var presentation = new PresentationCollection { PublicId = "https://test.example/5" };

        presentation.PublicIdIsRoot(baseUrl, 5).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://test.example/6", "Different customer")]
    [InlineData("https://different.example/5", "Different host")] // NOTE - this should pass
    [InlineData("https://test.example/collections/5", "Root flat id")]
    public void PublicIdIsRoot_False(string publicId, string because)
    {
        const string baseUrl = "https://test.example";
        var presentation = new PresentationCollection { PublicId = publicId };

        presentation.PublicIdIsRoot(baseUrl, 5).Should().BeFalse(because);
    }
}
