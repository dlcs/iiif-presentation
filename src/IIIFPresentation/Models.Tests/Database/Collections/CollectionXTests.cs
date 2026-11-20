using Core.Infrastructure;
using Models.Database.Collections;
#pragma warning disable CS8601 // Possible null reference assignment.

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

    [Fact]
    public void IsPublic_IsStorageCollection_False_IfNoBehaviors()
    {
        var collection = new Collection { Id = "test" };

        collection.IsPublic().Should().BeFalse();
        collection.IsStorageCollection().Should().BeFalse();
    }

    [Fact]
    public void IsPublic_IsStorageCollection_True_IfBehaviorsPresent()
    {
        var collection = new Collection
        {
            Id = "test",
            Behavior = [Behavior.IsPublic, Behavior.IsStorageCollection]
        };

        collection.IsPublic().Should().BeTrue();
        collection.IsStorageCollection().Should().BeTrue();
    }
}
