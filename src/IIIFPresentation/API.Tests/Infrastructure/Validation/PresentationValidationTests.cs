using API.Infrastructure.Validation;
using API.Tests.Helpers;
using Models.API;
using Models.Database.Collections;
using Models.Database.General;
using Repository.Paths;

namespace API.Tests.Infrastructure.Validation;

public class PresentationValidationTests
{
    private readonly IPathGenerator pathGenerator = TestPathGenerator.CreatePathGenerator("api.tests", Uri.UriSchemeHttps);
    private const string BaseUri = "https://api.tests";
    private const int Customer = 1;
    
    [Fact]
    public void IsUriParentInvalid_False_IfUriAndMatchesParent()
    {
        // Arrange
        var presentation = new TestPresentation { Parent = $"https://api.tests/{Customer}/collections/parent" };
        var parent = new Collection { Id = "parent", CustomerId = Customer };
        
        // Assert
        presentation.IsParentInvalid(parent, BaseUri, Customer, pathGenerator).Should().BeFalse();
    }
    
    [Fact]
    public void IsUriParentInvalid_True_IfUriAndDoesNotMatchParent()
    {
        // Arrange
        var presentation = new TestPresentation { Parent = $"https://api.tests/{Customer}/collections/not-parent" };
        var parent = new Collection { Id = "parent", CustomerId = Customer };
        
        // Assert
        presentation.IsParentInvalid(parent, BaseUri, Customer, pathGenerator).Should().BeTrue();
    }
    
    [Fact]
    public void IsUriParentInvalid_False_IfHierarchicalUriAndMatchesParent()
    {
        // Arrange
        var presentation = new TestPresentation { Parent = $"https://api.tests/{Customer}/hierarchy-parent" };
        var parent = new Collection { Id = "parent", CustomerId = Customer, Hierarchy =
            [
                new Hierarchy { Canonical = true, Slug = "hierarchy-parent", FullPath = "hierarchy-parent", CustomerId = 1 }
            ]
        };
        
        // Assert
        presentation.IsParentInvalid(parent, BaseUri, Customer, pathGenerator).Should().BeFalse();
    }
    
    [Fact]
    public void IsUriParentInvalid_True_IfHierarchicalUriAndDoesNotMatchParent()
    {
        // Arrange
        var presentation = new TestPresentation { Parent = "https://api.tests/not-parent" };
        var parent = new Collection { Id = "parent", CustomerId = Customer, Hierarchy =
            [
                new Hierarchy { Canonical = true, Slug = "hierarchy-parent", FullPath = "hierarchy-parent", CustomerId = Customer }
            ]
        };
        
        // Assert
        presentation.IsParentInvalid(parent, BaseUri, Customer, pathGenerator).Should().BeTrue();
    }
    
    [Fact]
    public void IsUriParentInvalid_ValidatesUriWithCollectionsAsHierarchy_IfHierarchicalUriWithCollectionsInIt()
    {
        // Arrange
        var presentation = new TestPresentation { Parent = "https://api.tests/1/parent/collections" };
        var parent = new Collection { Id = "parent", CustomerId = Customer, Hierarchy =
            [
                new Hierarchy { Canonical = true, Slug = "collections", FullPath = "parent/collections", CustomerId = Customer }
            ]
        };
        
        // Assert
        presentation.IsParentInvalid(parent, BaseUri, Customer, pathGenerator).Should().BeFalse();
    }
}

public class TestPresentation : IPresentation
{
    public string? PublicId { get; set; }
    public string? FlatId { get; set; }
    public string? Slug { get; set; }
    public string? Parent { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
