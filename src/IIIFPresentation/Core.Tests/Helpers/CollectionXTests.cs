using Core.Helpers;

namespace Core.Tests.Helpers;

public class CollectionXTests
{
    [Fact]
    public void IsNullOrEmpty_True_IfNull()
    {
        List<int> coll = null;

        coll.IsNullOrEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsNullOrEmpty_True_IfEmpty()
    {
        var coll = new List<int>();

        coll.IsNullOrEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsNullOrEmpty_False_IfHasValues()
    {
        var coll = new List<int> {2};

        coll.IsNullOrEmpty().Should().BeFalse();
    }
}
