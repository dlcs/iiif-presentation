using API.Converters;
using API.Infrastructure.Validation;
using Models.API;
using Models.Database.Collections;

namespace API.Tests.Infrastructure.Validation;

public class PresentationValidationTests
{
    private readonly UrlRoots urlRoots = new() { BaseUrl = "https://api.tests" };
    
    [Fact]
    public void IsUriParentInvalid_False_IfNotUri()
    {
        // Arrange
        var presentation = new TestPresentation { Parent = "foo" };
        var parent = new Collection { Id = "bar" };
        
        // Assert
        presentation.IsUriParentInvalid(parent, urlRoots).Should().BeFalse();
    }
    
    [Fact]
    public void IsUriParentInvalid_False_IfUriAndMatchesParent()
    {
        // Arrange
        var presentation = new TestPresentation { Parent = "https://api.tests/1/collections/parent" };
        var parent = new Collection { Id = "parent", CustomerId = 1 };
        
        // Assert
        presentation.IsUriParentInvalid(parent, urlRoots).Should().BeFalse();
    }
    
    [Fact]
    public void IsUriParentInvalid_True_IfUriAndDoesNotMatchParent()
    {
        // Arrange
        var presentation = new TestPresentation { Parent = "https://api.tests/not-parent" };
        var parent = new Collection { Id = "parent", CustomerId = 1 };
        
        // Assert
        presentation.IsUriParentInvalid(parent, urlRoots).Should().BeTrue();
    }
}

public class TestPresentation : IPresentation
{
    public string? Slug { get; set; }
    public string? Parent { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}