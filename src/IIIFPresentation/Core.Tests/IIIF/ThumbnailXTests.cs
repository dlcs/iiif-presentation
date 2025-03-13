using Core.IIIF;
using IIIF.Presentation.V3.Content;

namespace Core.Tests.IIIF;

public class ThumbnailXTests
{
    [Fact]
    public void GetThumbnailPath_RetrievesPathWhenSingleItem()
    {
        // Arrange
        var thumbnails = new List<Image>
        {
            new() { Id = "test" }
        };

        // Act
        var id = thumbnails.GetThumbnailPath();

        // Assert
        id.Should().Be("test");
    }
    
    [Fact]
    public void GetThumbnailPath_RetrievesPathWhenMultipleButWidthCorrect()
    {
        // Arrange
        var thumbnails = new List<Image>
        {
            new() { Id = "incorrect" },
            new() { Id = "correct", Width = 100 }
        };

        // Act
        var id = thumbnails.GetThumbnailPath();

        // Assert
        id.Should().Be("correct");
    }
    
    [Fact]
    public void GetThumbnailPath_RetrievesPathWhenMultipleButHeightCorrect()
    {
        // Arrange
        var thumbnails = new List<Image>
        {
            new() { Id = "correct", Height = 100 },
            new() { Id = "incorrect" },
        };

        // Act
        var id = thumbnails.GetThumbnailPath();

        // Assert
        id.Should().Be("correct");
    }
    
    [Fact]
    public void GetThumbnailPath_RetrievesClosestPathWhenMultiple()
    {
        // Arrange
        var thumbnails = new List<Image>
        {
            new() { Id = "incorrect", Width = 151 },
            new() { Id = "correct", Width = 50 },
        };
        
        // Act
        var id = thumbnails.GetThumbnailPath();

        // Assert
        id.Should().Be("correct");
    }
    
    [Fact]
    public void GetThumbnailPath_RetrievesClosestPathWhenMultiple_WithBothDimensions()
    {
        // Arrange
        var thumbnails = new List<Image>
        {
            new() { Id = "incorrect", Width = 151, Height = 50 },
            new() { Id = "correct", Width = 50, Height = 90 },
        };
        
        // Act
        var id = thumbnails.GetThumbnailPath();

        // Assert
        id.Should().Be("correct");
    }
    
    [Fact]
    public void GetThumbnailPath_AllowsNull()
    {
        // Arrange
        List<Image>? thumbnails = null;

        // Act
        var id = thumbnails!.GetThumbnailPath();

        // Assert
        id.Should().BeNull();
    }
}
