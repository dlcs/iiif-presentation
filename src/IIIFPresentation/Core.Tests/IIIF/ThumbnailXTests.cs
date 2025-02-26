﻿using Core.IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;

namespace Core.Tests.IIIF;

public class ThumbnailXTests
{
    [Fact]
    public void GetThumbnailPath_RetrievesPathWhenSingleItem()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "test",
            Thumbnail =
            [
                new Image
                {
                    Id = "test"
                }
            ]
        };
        
        var thumbnails = collection.Thumbnail.Select(x => x as Image).ToList(); 

        // Act
        var id = thumbnails!.GetThumbnailPath();

        // Assert
        id.Should().Be("test");
    }
    
    [Fact]
    public void GetThumbnailPath_RetrievesPathWhenMultipleButWidthCorrect()
    {
        // Arrange
        var collection = new Collection()
        {
            Id = "test",
            Thumbnail =
            [
                new Image
                {
                    Id = "incorrect"
                },

                new Image
                {
                    Id = "correct",
                    Width = 100
                }
            ]
        };

        var thumbnails = collection.Thumbnail.Select(x => x as Image).ToList();

        // Act
        var id = thumbnails!.GetThumbnailPath();

        // Assert
        id.Should().Be("correct");
    }
    
    [Fact]
    public void GetThumbnailPath_RetrievesPathWhenMultipleButHeightCorrect()
    {
        // Arrange
        var collection = new Collection()
        {
            Id = "test",
            Thumbnail =
            [
                new Image
                {
                    Id = "incorrect"
                },

                new Image
                {
                    Id = "correct",
                    Height = 100
                }
            ]
        };

        var thumbnails = collection.Thumbnail.Select(x => x as Image).ToList();

        // Act
        var id = thumbnails!.GetThumbnailPath();

        // Assert
        id.Should().Be("correct");
    }
    
    [Fact]
    public void GetThumbnailPath_RetrievesPathWhenMultipleButSingleValue()
    {
        // Arrange
        var collection = new Collection()
        {
            Id = "test",
            Thumbnail =
            [
                new Image
                {
                    Id = "incorrect"
                },

                new Image
                {
                    Id = "correct",
                    Width = 101
                }
            ]
        };

        var thumbnails = collection.Thumbnail.Select(x => x as Image).ToList();

        // Act
        var id = thumbnails!.GetThumbnailPath();

        // Assert
        id.Should().Be("correct");
    }
    
    [Fact]
    public void GetThumbnailPath_RetrievesClosestPathWhenMultiple()
    {
        // Arrange
        var collection = new Collection()
        {
            Id = "test",
            Thumbnail =
            [
                new Image
                {
                    Id = "incorrect",
                    Width = 151
                },

                new Image
                {
                    Id = "correct",
                    Width = 50
                }
            ]
        };

        var thumbnails = collection.Thumbnail.Select(x => x as Image).ToList();

        // Act
        var id = thumbnails!.GetThumbnailPath();

        // Assert
        id.Should().Be("correct");
    }
    
    [Fact]
    public void GetThumbnailPath_AllowsNull()
    {
        // Arrange
        var collection = new Collection()
        {
            Id = "test",
        };

        var thumbnails = collection.Thumbnail?.Select(x => x as Image).ToList();

        // Act
        var id = thumbnails!?.GetThumbnailPath();

        // Assert
        id.Should().BeNull();
    }
}