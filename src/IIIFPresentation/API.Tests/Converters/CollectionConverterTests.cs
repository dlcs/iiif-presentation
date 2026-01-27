using API.Converters;
using API.Features.Storage.Helpers;
using DLCS;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.Database.General;
using Repository.Paths;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
using Test.Helpers.Helpers;
using Collection = Models.Database.Collections.Collection;
using TestPathGenerator = API.Tests.Helpers.TestPathGenerator;

#nullable disable

namespace API.Tests.Converters;

public class CollectionConverterTests
{
    private const int PageSize = 100;
    private const int CustomerId = 1;
    private const int SettingsBasedRedirectCustomer = 10;
    
    private readonly IPathGenerator pathGenerator = TestPathGenerator.CreatePathGenerator("base", Uri.UriSchemeHttp);
    
    private readonly SettingsBasedPathGenerator settingsBasedPathGenerator = new(Options.Create(new DlcsSettings
    {
        ApiUri = new Uri("https://dlcs.api")
    }), new SettingsDrivenPresentationConfigGenerator(Options.Create(new PathSettings
    {
        PresentationApiUrl = new Uri("http://base"),
        CustomerPresentationApiUrl = new Dictionary<int, Uri>()
        {
            {SettingsBasedRedirectCustomer, new Uri("https://settings-based:7230")}
        },
        PathRules = PathRewriteOptions.Default
    })));

    [Fact]
    public void ToHierarchicalCollection_ConvertsStorageCollection()
    {
        // Arrange
        var storageRoot = new Collection
        {
            Id = "some-id",
            CustomerId = CustomerId,
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
                    Slug = "root",
                    CustomerId = CustomerId
                }
            ]
        };

        // Act
        var hierarchicalCollection =
            storageRoot.ToHierarchicalCollection(pathGenerator, CreateTestItems());
        // Assert
        hierarchicalCollection.Id.Should().Be("http://base/1");
        hierarchicalCollection.Label!.Should().HaveCount(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items!.Should().HaveCount(1);
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
        hierarchicalCollection.Label!.Should().HaveCount(1);
        hierarchicalCollection.Label["en"].Should().Contain("repository root");
        hierarchicalCollection.Items!.Should().HaveCount(1);
        hierarchicalCollection.Context!.Should().Be("http://iiif.io/api/presentation/3/context.json");
    }

    [Fact]
    public void ToPresentationCollection_NoSeeAlso_IfIIIFCollection()
    {
        // Arrange
        var collection = CreateTestHierarchicalCollection(false, true);

        // Act
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, CreateTestItems(), null, pathGenerator, settingsBasedPathGenerator);

        // Assert
        presentationCollection.SeeAlso.Should().BeNull();
    }
    
    [Fact]
    public void ToPresentationCollection_PublicAndApiSeeAlso_IfPublicStorageCollection()
    {
        // Arrange
        var collection = CreateTestHierarchicalCollection(true, true);

        // Act
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, CreateTestItems(), null, pathGenerator, settingsBasedPathGenerator);

        // Assert
        presentationCollection.SeeAlso.Should().HaveCount(2);
        presentationCollection.SeeAlso![0].Id.Should().Be("http://base/1/top/some-id");
        presentationCollection.SeeAlso[0].Profile.Should().Be("public-iiif");
        presentationCollection.SeeAlso[1].Id.Should().Be("http://base/1/top/some-id");
        presentationCollection.SeeAlso[1].Profile.Should().Be("api-hierarchical");
    }
    
    [Fact]
    public void ToPresentationCollection_ConvertsStorageCollection()
    {
        // Arrange
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = CustomerId,
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
                    CustomerId = CustomerId,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };

        var expectedCounts = new DescendantCounts(1, 0, 0);

        // Act
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, CreateTestItems(), null, pathGenerator, settingsBasedPathGenerator);

        // Assert
        presentationCollection.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be("http://base/1",
            "falls back to using the host based path generator for customer 1");
        presentationCollection.Label!.Should().HaveCount(1);
        presentationCollection.Label["en"].Should().Contain("repository root");
        presentationCollection.Slug.Should().Be("root");
        presentationCollection.SeeAlso.Should().HaveCount(1);
        presentationCollection.SeeAlso![0].Id.Should().Be("http://base/1");
        presentationCollection.SeeAlso[0].Profile.Should().Contain("api-hierarchical");
        presentationCollection.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Parent.Should().BeNull();
        presentationCollection.Items!.Should().HaveCount(1);
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
            storageRoot.ToPresentationCollection(PageSize, 1, 0, CreateTestItems(), null, pathGenerator, settingsBasedPathGenerator);

        // Assert
        presentationCollection.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be("http://base/1/top/some-id",
            "falls back to using the host based path generator for customer 1");
        presentationCollection.Label!.Should().HaveCount(1);
        presentationCollection.Label["en"].Should().Contain("repository root");
        presentationCollection.Slug.Should().Be("root");
        presentationCollection.SeeAlso.Should().HaveCount(1);
        presentationCollection.SeeAlso![0].Id.Should().Be("http://base/1/top/some-id");
        presentationCollection.SeeAlso![0].Profile.Should().Contain("api-hierarchical");
        presentationCollection.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Parent.Should().Be("http://base/1/collections/top");
        presentationCollection.Items!.Should().HaveCount(1);
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
            storageRoot.ToPresentationCollection(1, 2, 3, CreateTestItems(), null, pathGenerator, settingsBasedPathGenerator, "orderBy=created");

        // Assert
        presentationCollection.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be("http://base/1/top/some-id",
            "falls back to using the host based path generator for customer 1");
        presentationCollection.Label!.Should().HaveCount(1);
        presentationCollection.Label["en"].Should().Contain("repository root");
        presentationCollection.Slug.Should().Be("root");
        presentationCollection.SeeAlso.Should().HaveCount(1);
        presentationCollection.SeeAlso![0].Profile.Should().Contain("api-hierarchical");
        presentationCollection.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Parent.Should().Be("http://base/1/collections/top");
        presentationCollection.Items!.Should().HaveCount(1);
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
            storageRoot.ToPresentationCollection(PageSize, 1, 0, CreateTestItems(), parentCollection,pathGenerator, settingsBasedPathGenerator);

        // Assert
        presentationCollection.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be("http://base/1/top/some-id",
            "falls back to using the host based path generator for customer 1");
        presentationCollection.Label!.Should().HaveCount(1);
        presentationCollection.Label["en"].Should().Contain("repository root");
        presentationCollection.Slug.Should().Be("root");
        presentationCollection.SeeAlso.Should().HaveCount(1);
        presentationCollection.SeeAlso![0].Id.Should().Be("http://base/1/top/some-id");
        presentationCollection.SeeAlso![0].Profile.Should().Contain("api-hierarchical");
        presentationCollection.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Parent.Should().Be("http://base/1/collections/top");
        presentationCollection.Items!.Should().HaveCount(1);
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
            CustomerId = CustomerId,
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            IsStorageCollection = true,
            Hierarchy = [
                new Hierarchy
                {
                    CollectionId = "some-id",
                    Slug = "root",
                    CustomerId = CustomerId,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };

        // Act
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, items, null,pathGenerator, settingsBasedPathGenerator);

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
            CustomerId = CustomerId,
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            IsStorageCollection = false,
            Hierarchy = [
                new Hierarchy
                {
                    CollectionId = "some-id",
                    Slug = "root",
                    CustomerId = CustomerId,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        };

        // Act
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, items, null,pathGenerator, settingsBasedPathGenerator);

        // Assert
        presentationCollection.Totals.Should().BeNull();
    }
    
    [Fact]
    public void SetGeneratedFields_SetsCorrectFields_WhenItemsNull()
    {
        // Arrange
        var  presentationCollection = CreateTestPresentationCollection();
        var parentCollection = new Collection { Id = "theparent", Label = new LanguageMap("none", "grace") };

        presentationCollection.PartOf =
        [
            new Canvas
            {
                Id = "some-id",
            }
        ];

        // Act
        var presentationCollectionWithFields =
            presentationCollection.SetIIIFGeneratedFields(CreateTestHierarchicalCollection(false),
                parentCollection,pathGenerator, settingsBasedPathGenerator);

        presentationCollectionWithFields.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollectionWithFields.PublicId.Should().Be("http://base/1/top/some-id",
            "falls back to using the host based path generator for customer 1");
        presentationCollectionWithFields.Parent.Should().Be("http://base/1/collections/top");
        presentationCollectionWithFields.Label!.Should().HaveCount(1);
        presentationCollectionWithFields.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Modified.Should().Be(DateTime.MinValue);
        presentationCollectionWithFields.Items.Should().BeNull();
        presentationCollectionWithFields.View.Should().BeNull();
        presentationCollectionWithFields.Totals.Should().BeNull();
        presentationCollectionWithFields.Behavior.IsPublic().Should().BeFalse();
    }
    
    [Fact]
    public void SetGeneratedFields_SetsCorrectFields_WhenItemsEmpty()
    {
        // Arrange
        var  presentationCollection = CreateTestPresentationCollection(0);
        var parentCollection = new Collection { Id = "theparent", Label = new LanguageMap("none", "grace") };

        // Act
        var presentationCollectionWithFields =
            presentationCollection.SetIIIFGeneratedFields(CreateTestHierarchicalCollection(false, true), parentCollection,pathGenerator, settingsBasedPathGenerator);

        presentationCollectionWithFields.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollectionWithFields.PublicId.Should().Be("http://base/1/top/some-id",
            "falls back to using the host based path generator for customer 1");
        presentationCollectionWithFields.Parent.Should().Be("http://base/1/collections/top");
        presentationCollectionWithFields.Label!.Should().HaveCount(1);
        presentationCollectionWithFields.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Modified.Should().Be(DateTime.MinValue);
        presentationCollectionWithFields.Items.Should().BeEmpty();
        presentationCollectionWithFields.View.Should().BeNull();
        presentationCollectionWithFields.TotalItems.Should().BeNull();
        presentationCollectionWithFields.Totals.Should().BeNull();
        presentationCollectionWithFields.Behavior.IsPublic().Should().BeTrue();
    }
    
    [Fact]
    public void SetGeneratedFields_SetsCorrectFields_WhenItems()
    {
        // Arrange
        var  presentationCollection = CreateTestPresentationCollection(5);
        var parentCollection = new Collection { Id = "theparent", Label = new LanguageMap("none", "grace") };

        // Act
        var presentationCollectionWithFields =
            presentationCollection.SetIIIFGeneratedFields(CreateTestHierarchicalCollection(false, true),  parentCollection,pathGenerator, settingsBasedPathGenerator);

        presentationCollectionWithFields.Id.Should().Be("http://base/1/collections/some-id");
        presentationCollectionWithFields.PublicId.Should().Be("http://base/1/top/some-id",
            "falls back to using the host based path generator for customer 1");
        presentationCollectionWithFields.Parent.Should().Be("http://base/1/collections/top");
        presentationCollectionWithFields.Label!.Should().HaveCount(1);
        presentationCollectionWithFields.Created.Should().Be(DateTime.MinValue);
        presentationCollection.Modified.Should().Be(DateTime.MinValue);
        presentationCollectionWithFields.Items.Should().HaveCount(5);
        presentationCollectionWithFields.View.Should().BeNull();
        presentationCollectionWithFields.TotalItems.Should().BeNull();
        presentationCollectionWithFields.Totals.Should().BeNull();
        presentationCollectionWithFields.SeeAlso.Should().BeNull();
        presentationCollectionWithFields.PartOf![0].Id.Should().Be("http://base/0/collections/theparent");
        presentationCollectionWithFields.Behavior.IsPublic().Should().BeTrue();
    }
    
    [Fact]
    public void ToPresentationCollection_ConvertsCollectionWithRedirectedPublicId_WhenUsingSettingsBasedCustomer()
    {
        // Arrange
        var collection = CreateTestHierarchicalCollection(customerId: SettingsBasedRedirectCustomer);

        // Act
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, CreateTestItems(), null, pathGenerator, settingsBasedPathGenerator);

        // Assert
        presentationCollection.Id.Should().Be($"http://base/{SettingsBasedRedirectCustomer}/collections/some-id");
        presentationCollection.FlatId.Should().Be("some-id");
        presentationCollection.PublicId.Should().Be($"https://settings-based:7230/{SettingsBasedRedirectCustomer}/top/some-id",
            "using the settings based path generator");
    }
    
    [Fact]
    public void ToPresentationCollection_PublicAndApiSeeAlsoWithSettingsBasedRedirects_IfUsingSettingsBasedRedirectsCustomer()
    {
        // Arrange
        var collection = CreateTestHierarchicalCollection(true, true, SettingsBasedRedirectCustomer);

        // Act
        var presentationCollection =
            collection.ToPresentationCollection(PageSize, 1, 1, CreateTestItems(), null, pathGenerator, settingsBasedPathGenerator);

        // Assert
        presentationCollection.SeeAlso.Should().HaveCount(2);
        presentationCollection.SeeAlso![0].Id.Should().Be("https://settings-based:7230/10/top/some-id", "using the settings based path generator");
        presentationCollection.SeeAlso[0].Profile.Should().Be("public-iiif");
        presentationCollection.SeeAlso[1].Id.Should().Be("https://settings-based:7230/10/top/some-id", "using the settings based path generator");
        presentationCollection.SeeAlso[1].Profile.Should().Be("api-hierarchical");
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
                CustomerId = CustomerId,
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

    private static Collection CreateTestHierarchicalCollection(bool isStorageCollection = true, bool isPublic = false, int? customerId = null)
    {
        var collection = new Collection
        {
            Id = "some-id",
            CustomerId = customerId ?? CustomerId,
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
            IsStorageCollection = isStorageCollection,
            IsPublic = isPublic,
            Hierarchy =
            [
                new Hierarchy
                {
                    CollectionId = "some-id",
                    Slug = "root",
                    Parent = "top",
                    CustomerId = customerId ?? CustomerId,
                    Type = isStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection,
                    Canonical = true,
                    FullPath = "top/some-id"
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
            CustomerId = CustomerId,
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
                    Slug = "some-slug",
                    CustomerId = CustomerId,
                    FullPath = "top/some-id",
                }
            ]
        };
        
        return collection;
    }
    
    private static PresentationCollection CreateTestPresentationCollection(int? numberOfItems = null)
    {
        var collection = new PresentationCollection
        {
            Id = "some-id",
            Label = new LanguageMap
            {
                { "en", new List<string> { "repository root" } }
            },
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue,
        };

        if (!numberOfItems.HasValue) return collection;
        
        var currentItem = 0;
        collection.Items = [];

        while (currentItem < numberOfItems)
        {
            collection.Items.Add(new IIIF.Presentation.V3.Collection
            {
                Id = $"some-id-{currentItem}",
            });
            currentItem++;
        }

        return collection;
    }
}
