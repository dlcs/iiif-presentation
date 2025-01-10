
using API.Features.Storage.Helpers;
using Models.Infrastucture;

namespace API.Tests.Features.Storage.Helpers;

public class BehaviourXTests
{
    [Fact]
    public void Behaviors_ShouldBeFalseWhenEmptyList()
    {
        // Arrange
        var behaviors = new List<string>();
        
        behaviors.IsPublic().Should().BeFalse();
        behaviors.IsStorageCollection().Should().BeFalse();
    }
    
    [Fact]
    public void Behaviors_ShouldBeTrueWhenExists()
    {
        // Arrange
        var behaviors = new List<string>()
        {
            Behavior.IsPublic,
            Behavior.IsStorageCollection
        };
        
        behaviors.IsPublic().Should().BeTrue();
        behaviors.IsStorageCollection().Should().BeTrue();
    }
    
    [Fact]
    public void Behaviors_ShouldBeFalseWhenNull()
    {
        // Arrange
        List<string>? behaviors = null;
        
        behaviors.IsPublic().Should().BeFalse();
        behaviors.IsStorageCollection().Should().BeFalse();
    }
}
