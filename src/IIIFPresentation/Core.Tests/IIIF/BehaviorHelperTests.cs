using Core.IIIF;
using Core.Infrastructure;

namespace Core.Tests.IIIF;

public class BehaviorHelperTests
{
    [Fact]
    public void OnlyPresentationBehaviours_ReturnsEmpty_WhenNull()
    {
        // Arrange
        List<string>? behaviours = null;

        // Act
        var result = behaviours.OnlyPresentationBehaviours();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void OnlyPresentationBehaviours_ReturnsEmpty_WhenNoPresentationBehaviours()
    {
        // Arrange
        List<string> behaviours = ["paged", "auto-advance"];

        // Act
        var result = behaviours.OnlyPresentationBehaviours();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void OnlyPresentationBehaviours_KeepsOnlyPresentationBehaviours()
    {
        // Arrange
        List<string> behaviours = ["paged", Behavior.IsPublic, "auto-advance", Behavior.IsStorageCollection];

        // Act
        var result = behaviours.OnlyPresentationBehaviours();

        // Assert
        result.Should().BeEquivalentTo([Behavior.IsPublic, Behavior.IsStorageCollection],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void IsPublic_IsStorageCollection_False_WhenEmpty()
    {
        // Arrange
        List<string> behaviors = [];

        // Act and Assert
        behaviors.IsPublic().Should().BeFalse();
        behaviors.IsStorageCollection().Should().BeFalse();
    }

    [Fact]
    public void IsPublic_IsStorageCollection_True_WhenPresent()
    {
        // Arrange
        List<string> behaviors = [Behavior.IsPublic, Behavior.IsStorageCollection];

        // Act and Assert
        behaviors.IsPublic().Should().BeTrue();
        behaviors.IsStorageCollection().Should().BeTrue();
    }

    [Fact]
    public void IsPublic_IsStorageCollection_False_WhenNull()
    {
        // Arrange
        List<string>? behaviors = null;

        // Act and Assert
        behaviors.IsPublic().Should().BeFalse();
        behaviors.IsStorageCollection().Should().BeFalse();
    }
}
