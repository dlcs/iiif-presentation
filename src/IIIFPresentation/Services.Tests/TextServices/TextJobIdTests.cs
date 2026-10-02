using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextJobIdTests
{
    [Fact]
    public void ToString_RendersExpectedFormat()
    {
        var jobId = new TextJobId(99, "my-manifest");

        jobId.ToString().Should().Be("99/iiif/my-manifest");
    }

    [Fact]
    public void FromString_ParsesValidJobId()
    {
        var jobId = TextJobId.FromString("99/iiif/my-manifest");

        jobId.CustomerId.Should().Be(99);
        jobId.ResourceId.Should().Be("my-manifest");
    }

    [Fact]
    public void FromString_RoundTrips()
    {
        const string value = "99/iiif/my-manifest";

        TextJobId.FromString(value).ToString().Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc/iiif/my-manifest")] // non-int customer
    [InlineData("99/iiif")]              // missing resource
    [InlineData("99/iiif/")]            // empty resource
    [InlineData("99/foo/my-manifest")]  // wrong separator segment
    [InlineData("99/my-manifest")]      // too few segments
    [InlineData("99/iiif/a/b")]         // too many segments
    public void FromString_Throws_ForMalformedInput(string value)
    {
        var act = () => TextJobId.FromString(value);

        act.Should().Throw<TextJobIdException>();
    }

    [Fact]
    public void TryParse_ReturnsTrue_AndPopulatesJobId_ForValidInput()
    {
        var success = TextJobId.TryParse("99/iiif/my-manifest", out var jobId);

        success.Should().BeTrue();
        jobId.Should().NotBeNull();
        jobId!.CustomerId.Should().Be(99);
        jobId.ResourceId.Should().Be("my-manifest");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc/iiif/my-manifest")]
    [InlineData("99/iiif")]
    [InlineData("99/iiif/")]
    [InlineData("99/foo/my-manifest")]
    public void TryParse_ReturnsFalse_AndNull_ForMalformedInput(string? value)
    {
        var success = TextJobId.TryParse(value, out var jobId);

        success.Should().BeFalse();
        jobId.Should().BeNull();
    }

    [Fact]
    public void Equality_TwoJobIdsWithSameComponents_AreEqual()
    {
        var a = new TextJobId(99, "my-manifest");
        var b = new TextJobId(99, "my-manifest");

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_TwoJobIdsWithDifferentComponents_AreNotEqual()
    {
        var a = new TextJobId(99, "my-manifest");
        var b = new TextJobId(99, "other-manifest");
        var c = new TextJobId(1, "my-manifest");

        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
        (a == c).Should().BeFalse();
    }
}
