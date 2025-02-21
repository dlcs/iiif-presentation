using API.Features.Common.Helpers;
using Models.API.Collection;
using Models.Database.Collections;

namespace API.Tests.Features.Common.Helpers;

public class ParentValidatorTests
{
    [Fact]
    public void ValidateParentCollection_NoErrors()
    {
        // Arrange
        var parentCollection = new Collection()
        {
            Id = "someId",
            IsStorageCollection = true
        };
        
        // Act
        var parentCollectionError = ParentValidator.ValidateParentCollection<PresentationCollection>(parentCollection);

        // Assert
        parentCollectionError.Should().BeNull();
    }
    
    [Fact]
    public void ValidateParentCollection_Error_WhenIiifCollection()
    {
        // Arrange
        var parentCollection = new Collection()
        {
            Id = "someId"
        };
        
        // Act
        var parentCollectionError = ParentValidator.ValidateParentCollection<PresentationCollection>(parentCollection);

        // Assert
        parentCollectionError.Should().NotBeNull();
    }
    
    [Fact]
    public void ValidateParentCollection_Error_WhenParentNull()
    {
        // Arrange and Act
        var parentCollectionError = ParentValidator.ValidateParentCollection<PresentationCollection>(null);

        // Assert
        parentCollectionError.Should().NotBeNull();
    }
}
