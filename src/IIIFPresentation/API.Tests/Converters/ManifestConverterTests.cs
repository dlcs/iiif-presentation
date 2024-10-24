using API.Converters;
using Models.API.Manifest;
using Models.Database.General;
using DBManifest = Models.Database.Collections.Manifest;

namespace API.Tests.Converters;

public class ManifestConverterTests
{
    [Fact]
    public void SetGeneratedFields_AddsCustomContext()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
        };

        var expectedContexts = new List<string>
        {
            "http://tbc.org/iiif-repository/1/context.json",
            "http://iiif.io/api/presentation/3/context.json"
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, new UrlRoots());

        // Assert
        result.Context.As<List<string>>().Should().BeEquivalentTo(expectedContexts);
    }
    
    [Fact]
    public void SetGeneratedFields_SetsId()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, new UrlRoots());

        // Assert
        result.Id.Should().Be("/123/manifests/id");
    }
    
    [Fact]
    public void SetGeneratedFields_SetsAuditFields()
    {
        // Arrange
        var iiifManifest = new PresentationManifest();
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow.AddDays(1),
            CreatedBy = "creator",
            ModifiedBy = "modifier",
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "slug" }],
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, new UrlRoots());

        // Assert
        result.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        result.Modified.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromSeconds(2));
        
        result.CreatedBy.Should().Be("creator");
        result.ModifiedBy.Should().Be("modifier");
    }
    
    [Fact]
    public void SetGeneratedFields_SetsParentAndSlug_FromSingleHierarchyByDefault()
    {
        // Arrange
        var iiifManifest = new PresentationManifest
        {
            Parent = "parent-will-be-overriden",
            Slug = "slug-will-be-overriden",
        };
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow.AddDays(1),
            CreatedBy = "creator",
            ModifiedBy = "modifier",
            Id = "id",
            Hierarchy = [new Hierarchy { Slug = "hierarchy-slug", Parent = "hierarchy-parent" }],
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, new UrlRoots());

        // Assert
        result.Slug.Should().Be("hierarchy-slug");
        result.Parent.Should().Be("/0/collections/hierarchy-parent", "Always use FlatId");
    }
    
    [Fact]
    public void SetGeneratedFields_SetsParentAndSlug_FromHierarchyUsingFactory()
    {
        // Arrange
        var iiifManifest = new PresentationManifest
        {
            Parent = "parent-will-be-overriden",
            Slug = "slug-will-be-overriden",
        };
        var dbManifest = new DBManifest
        {
            CustomerId = 123,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow.AddDays(1),
            CreatedBy = "creator",
            ModifiedBy = "modifier",
            Id = "id",
            Hierarchy = [
                new Hierarchy { Slug = "hierarchy-slug", Parent = "hierarchy-parent" },
                new Hierarchy { Slug = "other-slug", Parent = "other-parent" },],
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, new UrlRoots(), manifest => manifest.Hierarchy.Last());

        // Assert
        result.Slug.Should().Be("other-slug");
        result.Parent.Should().Be("/0/collections/other-parent", "Always use FlatId");
    } 
}