using System.Runtime.InteropServices;
using API.Infrastructure.Helpers;

namespace API.Tests.Infrastructure.Helpers;

public class EtagComparerTests
{
    [Fact]
    public void IsMatch_False_ForEmpty()
    {
        EtagComparer.IsMatch(Guid.NewGuid(), string.Empty).Should().BeFalse("empty string can't match");
    }
    
    [Fact]
    public void IsMatch_False_ForNull()
    {
        EtagComparer.IsMatch(Guid.NewGuid(), null).Should().BeFalse("null string can't match");
    }
    
    [Theory]
    [InlineData("not a guid")]
    [InlineData("'7f9a664c3de845188ec7c3e62846f5a0'")] // `'` is not valid for ETag string
    [InlineData("\"fd671e6abc0e42e5ab72c6819e37504\"")] // too short
    public void IsMatch_False_ForInvalid(string invalid)
    {
        EtagComparer.IsMatch(Guid.NewGuid(), invalid).Should().BeFalse("is not a valid ETag/Guid");
    }
    
    [Theory]
    [InlineData("ca5b5be15419427bac32b110d6790e46", "ca5b5be15419427bac32b110d6790e46")]
    [InlineData("193b17d4-e409-40b0-bd83-125e4e9cdde8", "193b17d4-e409-40b0-bd83-125e4e9cdde8")]
    [InlineData("e2f6eb68d21045749cc6c1e0d1c08921", "\"e2f6eb68d21045749cc6c1e0d1c08921\"")]
    public void IsMatch_True_ForValid(string guidStr, string incoming)
    {
        var guid = Guid.Parse(guidStr);
        EtagComparer.IsMatch(guid, incoming).Should().BeTrue();
    }
}
