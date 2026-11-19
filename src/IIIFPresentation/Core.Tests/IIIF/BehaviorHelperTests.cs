using Core.IIIF;
using Core.Infrastructure;

namespace Core.Tests.IIIF;

public class BehaviorHelperTests
{
    [Fact]
    public void OnlyPresentationBehaviours_ReturnsNull_WhenNull()
    {
        // Arrange
        List<string>? behaviours = null;

        // Act
        var result = behaviours.OnlyPresentationBehaviours();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void OnlyPresentationBehaviours_ReturnsNull_WhenNoPresentationBehaviours()
    {
        // Arrange
        List<string> behaviours = ["paged", "auto-advance"];

        // Act
        var result = behaviours.OnlyPresentationBehaviours();

        // Assert
        result.Should().BeNull();
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
}
