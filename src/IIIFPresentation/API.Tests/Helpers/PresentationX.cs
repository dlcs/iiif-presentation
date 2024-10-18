using API.Helpers;
using Models.API;

namespace API.Tests.Helpers;

public class PresentationX
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetParentSlug_Throws_IfParentNullOrEmpty(string? parentSlug)
    {
        var presentation = new TestPresentation { Parent = parentSlug!, Slug = "hi" };
        Action action = () => presentation.GetParentSlug();

        action.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("/foo")]
    [InlineData("bar/foo")]
    public void GetParentSlug_ReturnsLastPathElement(string parentSlug)
    {
        const string expected = "foo";
        var presentation = new TestPresentation { Parent = parentSlug!, Slug = "hi" };
        presentation.GetParentSlug().Should().Be(expected);
    }
    
    public class TestPresentation : IPresentation
    {
        public string? Slug { get; set; }
        public string? Parent { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
    }
}