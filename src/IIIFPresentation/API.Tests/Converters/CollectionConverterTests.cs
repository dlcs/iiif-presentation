﻿using API.Converters;
using API.Helpers;
using API.Tests.Helpers;
using IIIF.Presentation.V3.Strings;
using Models.API.Collection;
using Models.Database.Collections;
using Models.Database.General;

#nullable disable

namespace API.Tests.Converters;

public class CollectionConverterTests
{
    private const int PageSize = 100;
    
    private readonly IPathGenerator pathGenerator = TestPathGenerator.CreatePathGenerator("base", Uri.UriSchemeHttp);

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
            IsStorageCollection = true,
            Hierarchy = [
                new Hierarchy
                {
                    Slug = "root"
                }
            ]
        };

        // Act
        var hierarchicalCollection =
            storageRoot.ToHierarchicalCollection(pathGenerator, CreateTestItems());
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
            storageRoot.ToHierarchicalCollection(pathGenerator, CreateTestItems());
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1/top/some-id");
        hierarchicalCollection.Label!.Count.Should().Be(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items!.Count.Should().Be(1);
        hierarchicalCollection.Context!.Should().Be("http://iiif.io/api/presentation/3/context.json");
    }
    
    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection()
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
            IsStorageCollection = true,
            Hierarchy = [
                new Hierarchy
                {
                    CollectionId = "some-id",
                    Slug = "root",
                    CustomerId = 1,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };

        var expectedCounts = new DescendantCounts(1, 0, 0);

        // Act
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, CreateTestItems(), null, pathGenerator);

        // Assert
        presentationCollection.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be("http://base/1");
        presentationCollection.Label!.Count.Should().Be(1);
        presentationCollection.Label["en"].Should().Contain("repository root");
        presentationCollection.Slug.Should().Be("root");
        presentationCollection.SeeAlso.Should().HaveCount(2);
        presentationCollection.SeeAlso![0].Id.Should().Be("http://base/1");
        presentationCollection.SeeAlso![0].Profile.Should().Contain("Public");
        presentationCollection.SeeAlso![1].Id.Should().Be("http://base/1/iiif");
        presentationCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        presentationCollection.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Parent.Should().BeNull();
        presentationCollection.Items!.Count.Should().Be(1);
        presentationCollection.View!.Id.Should().Be("http://base/1/collections/some-id?page=1&pageSize=100");
        presentationCollection.View.Next.Should().BeNull();
        presentationCollection.View.Last.Should().BeNull();
        presentationCollection.PartOf.Should().BeNull("No parent provided");
        presentationCollection.Totals.Should().BeEquivalentTo(expectedCounts);
    }
    
    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection_WithFullPath()
    {
        // Arrange
        var storageRoot = CreateTestHierarchicalCollection();
        var expectedCounts = new DescendantCounts(1, 0, 0);

        // Act
        var presentationCollection =
            storageRoot.ToPresentationCollection(PageSize, 1, 0, CreateTestItems(), null, pathGenerator);

        // Assert
        presentationCollection.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be("http://base/1/top/some-id");
        presentationCollection.Label!.Count.Should().Be(1);
        presentationCollection.Label["en"].Should().Contain("repository root");
        presentationCollection.Slug.Should().Be("root");
        presentationCollection.SeeAlso.Should().HaveCount(2);
        presentationCollection.SeeAlso![0].Id.Should().Be("http://base/1/top/some-id");
        presentationCollection.SeeAlso![0].Profile.Should().Contain("Public");
        presentationCollection.SeeAlso![1].Id.Should().Be("http://base/1/top/some-id/iiif");
        presentationCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        presentationCollection.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Parent.Should().Be("http://base/1/collections/top");
        presentationCollection.Items!.Count.Should().Be(1);
        presentationCollection.View!.Id.Should().Be("http://base/1/collections/some-id?page=1&pageSize=100");
        presentationCollection.View.Next.Should().BeNull();
        presentationCollection.View.Last.Should().BeNull();
        presentationCollection.View.First.Should().BeNull();
        presentationCollection.View.Next.Should().BeNull();
        presentationCollection.PartOf.Should().BeNull("No parent provided");
        presentationCollection.Totals.Should().BeEquivalentTo(expectedCounts);
    }

    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection_WithCorrectPaging()
    {
        // Arrange
        var storageRoot = CreateTestHierarchicalCollection();
        var expectedCounts = new DescendantCounts(1, 0, 0);

        // Act
        var presentationCollection =
            storageRoot.ToPresentationCollection(1, 2, 3, CreateTestItems(), null, pathGenerator, "orderBy=created");

        // Assert
        presentationCollection.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be("http://base/1/top/some-id");
        presentationCollection.Label!.Count.Should().Be(1);
        presentationCollection.Label["en"].Should().Contain("repository root");
        presentationCollection.Slug.Should().Be("root");
        presentationCollection.SeeAlso.Should().HaveCount(2);
        presentationCollection.SeeAlso![0].Profile.Should().Contain("Public");
        presentationCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        presentationCollection.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Parent.Should().Be("http://base/1/collections/top");
        presentationCollection.Items!.Count.Should().Be(1);
        presentationCollection.View!.TotalPages.Should().Be(3);
        presentationCollection.View.PageSize.Should().Be(1);
        presentationCollection.View.Id.Should().Be("http://base/1/collections/some-id?page=2&pageSize=1&orderBy=created");
        presentationCollection.View.Next.Should().Be("http://base/1/collections/some-id?page=3&pageSize=1&orderBy=created");
        presentationCollection.View.Previous.Should().Be("http://base/1/collections/some-id?page=1&pageSize=1&orderBy=created");
        presentationCollection.View.First.Should().Be("http://base/1/collections/some-id?page=1&pageSize=1&orderBy=created");
        presentationCollection.View.Last.Should().Be("http://base/1/collections/some-id?page=3&pageSize=1&orderBy=created");
        presentationCollection.TotalItems.Should().Be(3);
        presentationCollection.PartOf.Should().BeNull("No parent provided");
        presentationCollection.Totals.Should().BeEquivalentTo(expectedCounts);
    }
    
    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection_IncludingPartOfForParent()
    {
        // Arrange
        var storageRoot = CreateTestHierarchicalCollection();
        var parentCollection = new Collection { Id = "theparent", Label = new LanguageMap("none", "grace") };
        var expectedCounts = new DescendantCounts(1, 0, 0);

        // Act
        var presentationCollection =
            storageRoot.ToPresentationCollection(PageSize, 1, 0, CreateTestItems(), parentCollection, pathGenerator);

        // Assert
        presentationCollection.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be("http://base/1/top/some-id");
        presentationCollection.Label!.Count.Should().Be(1);
        presentationCollection.Label["en"].Should().Contain("repository root");
        presentationCollection.Slug.Should().Be("root");
        presentationCollection.SeeAlso.Should().HaveCount(2);
        presentationCollection.SeeAlso![0].Id.Should().Be("http://base/1/top/some-id");
        presentationCollection.SeeAlso![0].Profile.Should().Contain("Public");
        presentationCollection.SeeAlso![1].Id.Should().Be("http://base/1/top/some-id/iiif");
        presentationCollection.SeeAlso[1].Profile.Should().Contain("api-hierarchical");
        presentationCollection.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Parent.Should().Be("http://base/1/collections/top");
        presentationCollection.Items!.Count.Should().Be(1);
        presentationCollection.View!.Id.Should().Be("http://base/1/collections/some-id?page=1&pageSize=100");
        presentationCollection.View.Next.Should().BeNull();
        presentationCollection.View.Last.Should().BeNull();
        presentationCollection.View.First.Should().BeNull();
        presentationCollection.View.Next.Should().BeNull();
        var partOf = presentationCollection.PartOf.Single();
        partOf.Id.Should().Be("http://base/0/collections/theparent");
        partOf.Label.Should().BeEquivalentTo(parentCollection.Label);
        
        presentationCollection.Totals.Should().BeEquivalentTo(expectedCounts);
    }
    
    [Theory]
    [MemberData(nameof(ItemsForTotals))]
    public void ToPresentationCollection_StorageCollection_HasCorrectTotals(List<Hierarchy> items, DescendantCounts expectedCounts)
    {
        // Arrange
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            IsStorageCollection = true,
            Hierarchy = [
                new Hierarchy
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
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, items, null, pathGenerator);

        // Assert
        presentationCollection.Totals.Should().BeEquivalentTo(expectedCounts);
    }
    
    [Theory]
    [MemberData(nameof(ItemsForTotals))]
    public void ToPresentationCollection_IIIFCollection_HasCorrectTotals(List<Hierarchy> items, DescendantCounts _)
    {
        // Arrange
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = 1,
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            IsStorageCollection = false,
            Hierarchy = [
                new Hierarchy
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
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, items, null, pathGenerator);

        // Assert
        presentationCollection.Totals.Should().BeNull();
    }

    public static IEnumerable<object[]> ItemsForTotals => new List<object[]>
    {
        new object[] { null, new DescendantCounts(0, 0, 0) },
        new object[] { new List<Hierarchy>(), new DescendantCounts(0, 0, 0) },
        new object[]
        {
            new List<Hierarchy>
            {
                new() { Slug = "1", Type = ResourceType.StorageCollection, Collection = new Collection { Id = "1" } },
            },
            new DescendantCounts(1, 0, 0)
        },
        new object[]
        {
            new List<Hierarchy>
            {
                new() { Slug = "1", Type = ResourceType.IIIFCollection, Collection = new Collection { Id = "1" } },
            },
            new DescendantCounts(0, 1, 0)
        },
        new object[]
        {
            new List<Hierarchy>
            {
                new() { Slug = "1", Type = ResourceType.IIIFManifest },
            },
            new DescendantCounts(0, 0, 1)
        },
        new object[]
        {
            new List<Hierarchy>
            {
                new() { Slug = "1", Type = ResourceType.IIIFManifest },
                new() { Slug = "2", Type = ResourceType.StorageCollection, Collection = new Collection { Id = "d" } },
                new() { Slug = "3", Type = ResourceType.IIIFCollection, Collection = new Collection { Id = "c" } },
                new() { Slug = "4", Type = ResourceType.StorageCollection, Collection = new Collection { Id = "b" } },
                new() { Slug = "5", Type = ResourceType.IIIFCollection, Collection = new Collection { Id = "a" } },
                new() { Slug = "6", Type = ResourceType.IIIFManifest },
            },
            new DescendantCounts(2, 2, 2)
        },
    };

    private static List<Hierarchy> CreateTestItems()
    {
        var items = new List<Hierarchy>
        {
            new()
            {
                CollectionId = "some-child",
                CustomerId = 1,
                Slug = "root",
                Type = ResourceType.StorageCollection,
                Collection = new Collection
                {
                    Id = "someId",
                    IsPublic = true,
                }
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
            IsStorageCollection = true,
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
            FullPath = "top/some-id",
            IsStorageCollection = true,
        };
        
        return collection;
    }
}
