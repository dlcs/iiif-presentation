using Models.Database.Collections;

namespace Models.Tests.Database.Collections;

public class CollectionXTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-root")]
    public void IsRoot_False_IfNotRoot(string? id)
        => new Collection { Id = id }.IsRoot().Should().BeFalse();

    [Theory]
    [InlineData("root")]
    [InlineData("ROOT")]
    public void IsRoot_True_IfRoot(string id)
        => new Collection { Id = id }.IsRoot().Should().BeTrue();
}
