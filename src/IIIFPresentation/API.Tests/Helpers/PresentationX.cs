using API.Helpers;
using Models.API.Collection.Upsert;

namespace API.Tests.Helpers;

public class PresentationX
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetParentSlug_Throws_IfParentNullOrEmpty(string? parentSlug)
    {
        var presentation = new UpsertFlatCollection { Parent = parentSlug!, Slug = "hi" };
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
        var presentation = new UpsertFlatCollection { Parent = parentSlug!, Slug = "hi" };
        presentation.GetParentSlug().Should().Be(expected);
    }
}