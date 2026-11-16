using Core.IIIF;
using Core.Infrastructure;

namespace Core.Tests.IIIF;

public class BehaviorHelperTests
{
    [Fact]
    public void WithoutPresentationBehaviours_ReturnsNull_WhenNull()
    {
        // Arrange
        List<string>? behaviours = null;

        // Act
        var result = behaviours.WithoutPresentationBehaviours();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void WithoutPresentationBehaviours_ReturnsNull_WhenOnlyPresentationBehaviours()
    {
        // Arrange
        List<string> behaviours = [Behavior.IsPublic, Behavior.IsStorageCollection];

        // Act
        var result = behaviours.WithoutPresentationBehaviours();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void WithoutPresentationBehaviours_RemovesPresentationBehaviours()
    {
        // Arrange
        List<string> behaviours = ["paged", Behavior.IsPublic, "auto-advance", Behavior.IsStorageCollection];

        // Act
        var result = behaviours.WithoutPresentationBehaviours();

        // Assert
        result.Should().BeEquivalentTo(["paged", "auto-advance"], o => o.WithStrictOrdering());
    }
}
