using API.Converters;
using Models.API.Manifest;
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
            Id = "id"
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
            Id = "id"
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
            Id = "id"
        };
        
        // Act
        var result = iiifManifest.SetGeneratedFields(dbManifest, new UrlRoots());

        // Assert
        result.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        result.Modified.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromSeconds(2));
        
        result.CreatedBy.Should().Be("creator");
        result.ModifiedBy.Should().Be("modifier");
    } 
}