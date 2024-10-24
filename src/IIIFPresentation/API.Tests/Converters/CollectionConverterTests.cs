﻿using API.Converters;
using IIIF.Presentation.V3.Strings;
using Models.Database.Collections;
using Models.Database.General;

#nullable disable

namespace API.Tests.Converters;

public class CollectionConverterTests
{
    private readonly UrlRoots urlRoots = new UrlRoots()
    {
        BaseUrl = "http://base"
    };

    private const int pageSize = 100;
    

    [Fact]
    public void ToHierarchicalCollection_ConvertsStorageCollection()
    {
        // Arrange
        var storageRoot = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            Hierarchy = [
                new Hierarchy()
                {
                    Slug = "root"
                }
            ]
        };

        // Act
        var hierarchicalCollection =
            storageRoot.ToHierarchicalCollection(urlRoots, CreateTestItems());
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1");
        hierarchicalCollection.Label!.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items!.Count.Should().Be(1);
        hierarchicalCollection.Context!.Should().Be("http://iiif.io/api/presentation/3/context.json");
    }
    
    [Fact]
    public void ToHierarchicalCollection_ConvertsStorageCollectionWithFullPath()
    {
        // Arrange
        var storageRoot = CreateTestCollection();

        // Act
        var hierarchicalCollection =
            storageRoot.ToHierarchicalCollection(urlRoots, CreateTestItems());
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1/top/some-id");
        hierarchicalCollection.Label!.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items!.Count.Should().Be(1);
        hierarchicalCollection.Context!.Should().Be("http://iiif.io/api/presentation/3/context.json");
    }
    
    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            Hierarchy = [
                new Hierarchy()
                {
                    CollectionId = "some-id",
                    Slug = "root",
                    CustomerId = 1,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
            
        };

        // Act
        var flatCollection =
            collection.ToFlatCollection(urlRoots, pageSize, 1, 1, CreateTestItems());

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.PublicId.Should().Be("http://base/1");
        flatCollection.Label!.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso![0].Id.Should().Be("http://base/1");
        flatCollection.SeeAlso![0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso![1].Id.Should().Be("http://base/1/iiif");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().BeNull();
        flatCollection.Items!.Count.Should().Be(1);
        flatCollection.View!.Id.Should().Be("http://base/1/collections/some-id?page=1&pageSize=100");
        flatCollection.View.Next.Should().BeNull();
        flatCollection.View.Last.Should().BeNull();
    }
    
    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection_WithFullPath()
    {
        // Arrange
        var storageRoot = CreateTestHierarchicalCollection();

        // Act
        var flatCollection =
            storageRoot.ToFlatCollection(urlRoots, pageSize, 1, 0, CreateTestItems());

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.PublicId.Should().Be("http://base/1/top/some-id");
        flatCollection.Label!.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso![0].Id.Should().Be("http://base/1/top/some-id");
        flatCollection.SeeAlso![0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso![1].Id.Should().Be("http://base/1/top/some-id/iiif");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().Be("http://base/1/collections/top");
        flatCollection.Items!.Count.Should().Be(1);
        flatCollection.View!.Id.Should().Be("http://base/1/collections/some-id?page=1&pageSize=100");
        flatCollection.View.Next.Should().BeNull();
        flatCollection.View.Last.Should().BeNull();
        flatCollection.View.First.Should().BeNull();
        flatCollection.View.Next.Should().BeNull();
    }

    [Fact]
    public void ToFlatCollection_ConvertsStorageCollection_WithCorrectPaging()
    {
        // Arrange
        var storageRoot = CreateTestHierarchicalCollection();

        // Act
        var flatCollection =
            storageRoot.ToFlatCollection(urlRoots, 1, 2, 3, CreateTestItems(), "orderBy=created");

        // Assert
        flatCollection.Id.Should().Be("http://base/1/collections/some-id");
        flatCollection.PublicId.Should().Be("http://base/1/top/some-id");
        flatCollection.Label!.Count.Should().Be(1);
        flatCollection.Label["en"].Should().Contain("repository root");
        flatCollection.Slug.Should().Be("root");
        flatCollection.SeeAlso.Should().HaveCount(2);
        flatCollection.SeeAlso![0].Profile.Should().Contain("Public");
        flatCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        flatCollection.Created.Should().Be(DateTime.MinValue);
        flatCollection.Parent.Should().Be("http://base/1/collections/top");
        flatCollection.Items!.Count.Should().Be(1);
        flatCollection.View!.TotalPages.Should().Be(3);
        flatCollection.View.PageSize.Should().Be(1);
        flatCollection.View.Id.Should().Be("http://base/1/collections/some-id?page=2&pageSize=1&orderBy=created");
        flatCollection.View.Next.Should().Be("http://base/1/collections/some-id?page=3&pageSize=1&orderBy=created");
        flatCollection.View.Previous.Should().Be("http://base/1/collections/some-id?page=1&pageSize=1&orderBy=created");
        flatCollection.View.First.Should().Be("http://base/1/collections/some-id?page=1&pageSize=1&orderBy=created");
        flatCollection.View.Last.Should().Be("http://base/1/collections/some-id?page=3&pageSize=1&orderBy=created");
        flatCollection.TotalItems.Should().Be(3);
    }

    private static List<Hierarchy> CreateTestItems()
    {
        var items = new List<Hierarchy>
        {
            new()
            {
                CollectionId = "some-child",
                CustomerId = 1,
                Slug = "root",
                Type = ResourceType.StorageCollection
            }
        };
        
        return items;
    }
    
    private static Collection CreateTestHierarchicalCollection()
    {
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            FullPath = "top/some-id",
            Hierarchy = [
                new Hierarchy()
                {
                    CollectionId = "some-id",
                    Slug = "root",
                    Parent = "top",
                    CustomerId = 1,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };
        
        return collection;
    }
    
    private static Collection CreateTestCollection()
    {
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            FullPath = "top/some-id"
        };
        
        return collection;
    }
}