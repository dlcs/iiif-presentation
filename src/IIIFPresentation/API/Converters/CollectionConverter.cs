using API.Helpers;
using Core.Helpers;
using Core.IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Models.API.Collection;
using Models.Database.General;
using Models.Infrastucture;
using Collection = IIIF.Presentation.V3.Collection;
using Manifest = IIIF.Presentation.V3.Manifest;
using DbCollection = Models.Database.Collections.Collection;

namespace API.Converters;

public static class CollectionConverter
{
    public static Collection ToHierarchicalCollection(this DbCollection dbAsset,
        IPathGenerator pathGenerator, List<Hierarchy>? items)
    {
        var collection = new Collection
        {
            Id = pathGenerator.GenerateHierarchicalCollectionId(dbAsset),
            Label = dbAsset.Label,
            Items = items?.Count > 0
                ? items.Select(i => GenerateCollectionItem(i, pathGenerator, false)).ToList()
                : []
        };

        collection.EnsurePresentation3Context();

        return collection;
    }

    public static PresentationCollection ToPresentationCollection(this DbCollection dbAsset,
        int pageSize, int currentPage, int totalItems, IList<Hierarchy>? items, DbCollection? parentCollection,
        IPathGenerator pathGenerator, string? orderQueryParam = null)
    {
        var totalPages = (int)Math.Ceiling(totalItems == 0 ? 1 : (double)totalItems / pageSize);
        items ??= [];

        var orderQueryParamConverted = string.IsNullOrEmpty(orderQueryParam) ? string.Empty : $"&{orderQueryParam}";
        var hierarchy = dbAsset.Hierarchy!.Single(h => h.Canonical);

        return new()
        {
            Id = pathGenerator.GenerateFlatCollectionId(dbAsset),
            Context = GenerateContext(),
            Label = dbAsset.Label,
            FlatId = dbAsset.Id,
            PublicId = pathGenerator.GenerateHierarchicalCollectionId(dbAsset),
            Behavior = GenerateBehavior(dbAsset),
            Slug = hierarchy.Slug,
            Parent = GeneratePresentationCollectionParent(pathGenerator, hierarchy),
            ItemsOrder = hierarchy.ItemsOrder,
            Items = GenerateItems(pathGenerator, items),
            PartOf = GeneratePartOf(parentCollection, pathGenerator),
            TotalItems = totalItems,
            View = GenerateView(dbAsset, pathGenerator, pageSize, currentPage, totalPages, orderQueryParamConverted),
            SeeAlso = GenerateSeeAlso(dbAsset, pathGenerator),
            Created = dbAsset.Created.Floor(DateTimeX.Precision.Second),
            Modified = dbAsset.Modified.Floor(DateTimeX.Precision.Second),
            CreatedBy = dbAsset.CreatedBy,
            ModifiedBy = dbAsset.ModifiedBy,
            Totals = GetDescendantCounts(dbAsset, items)
        };
    }

    private static ICollectionItem GenerateCollectionItem(Hierarchy hierarchy, IPathGenerator pathGenerator,
        bool flatId)
    {
        var id = flatId ? pathGenerator.GenerateFlatId(hierarchy) : pathGenerator.GenerateHierarchicalId(hierarchy);

        if (hierarchy.Type == ResourceType.IIIFManifest)
        {
            return new Manifest
            {
                Id = id,
                Label = hierarchy.Manifest?.Label,
            };
        }

        var collection = new Collection
        {
            Id = id,
            Label = hierarchy.Collection?.Label,
        };

        if (flatId) collection.Behavior = GenerateBehavior(hierarchy.Collection!);

        return collection;
    }

    /// <summary>
    /// Generate the total pages element of a presentation collection
    /// </summary>
    /// <param name="pageSize">The size of the page</param>
    /// <param name="totalItems">The total number of items</param>
    /// <returns>total pages</returns>
    public static int GenerateTotalPages(int pageSize, int totalItems)
    {
        return (int)Math.Ceiling(totalItems == 0 ? 1 : (double)totalItems / pageSize);
    }

    /// <summary>
    /// Generates a parent id for a collection in the presentation collection form
    /// </summary>
    /// <param name="pathGenerator">The path generator</param>
    /// <param name="hierarchy">The hierarchy to get the parent from</param>
    /// <returns>An id of the parent, in the presentation collection form</returns>
    public static string? GeneratePresentationCollectionParent(IPathGenerator pathGenerator, Hierarchy hierarchy)
    {
        return hierarchy.Parent != null
            ? pathGenerator.GenerateFlatParentId(hierarchy)
            : null;
    }

    /// <summary>
    /// Generates a context for a collection
    /// </summary>
    /// <returns>A list of strings</returns>
    public static List<string> GenerateContext()
    {
        return
        [
            PresentationJsonLdContext.Context,
            Context.Presentation3Context
        ];
    }

    /// <summary>
    /// Generates a list of behaviors for a collection
    /// </summary>
    /// <param name="collection">The database collection to use</param>
    /// <returns>A list of behaviors</returns>
    public static List<string>? GenerateBehavior(DbCollection collection)
    {
        var behaviours = new List<string>()
            .AppendIf(collection.IsPublic, Behavior.IsPublic)
            .AppendIf(collection.IsStorageCollection, Behavior.IsStorageCollection);

        return behaviours.Any() ? behaviours : null;
    }

    /// <summary>
    /// Generates the PartOf field for a collection
    /// </summary>
    /// <param name="collection">The collection required</param>
    /// <param name="pathGenerator">Class used to generate paths for collections</param>
    /// <returns>A list of ResourceBase</returns>
    public static List<ResourceBase>? GeneratePartOf(DbCollection? collection,
        IPathGenerator pathGenerator) =>
        collection != null
            ?
            [
                new ExternalResource(nameof(PresentationType.Collection))
                {
                    Id = pathGenerator.GenerateFlatCollectionId(collection),
                    Label = collection!.Label
                }
            ]
            : null;

    /// <summary>
    /// Generates the SeeAlso part of a collection
    /// </summary>
    /// <param name="collection">The collection to use in generation</param>
    /// <param name="pathGenerator">Generates paths for collections</param>
    /// <returns>A list of external resources</returns>
    public static List<ExternalResource> GenerateSeeAlso(Models.Database.Collections.Collection collection, 
        IPathGenerator pathGenerator)
    {
        return [
            new(nameof(PresentationType.Collection))
            {
                Id = pathGenerator.GenerateHierarchicalCollectionId(collection),
                Label = collection.Label,
                Profile = "Public"
            },

            new(nameof(PresentationType.Collection))
            {
                Id = $"{pathGenerator.GenerateHierarchicalCollectionId(collection)}/iiif",
                Label = collection.Label,
                Profile = "api-hierarchical"
            }
        ];
    }

    /// <summary>
    /// Generates items in a hierarchy into the correct format
    /// </summary>
    /// <param name="pathGenerator">Generates a path</param>
    /// <param name="items">The items to convert</param>
    /// <returns>A list of ICollectionItems</returns>
    public static List<ICollectionItem> GenerateItems(IPathGenerator pathGenerator, IEnumerable<Hierarchy> items)
    {
        return items.Select(i => GenerateCollectionItem(i, pathGenerator, true)).ToList();
    }

    /// <summary>
    /// Generates the view component of a presentation collection
    /// </summary>
    /// <param name="collection">The database collection to use in the convewrsion</param>
    /// <param name="pathGenerator">The path generator</param>
    /// <param name="pageSize">Sets the page size</param>
    /// <param name="currentPage">The current page that the view is being generated for</param>
    /// <param name="totalPages">How many pages to generate the View for</param>
    /// <param name="orderQueryParam">What the View is being ordered by</param>
    /// <returns>A View</returns>
    public static View GenerateView(Models.Database.Collections.Collection collection, IPathGenerator pathGenerator, int pageSize,
        int currentPage, int totalPages, string? orderQueryParam = null)
    {
        var view = new View()
        {
            Id = pathGenerator.GenerateFlatCollectionViewId(collection, currentPage, pageSize, orderQueryParam),
            Type = PresentationType.PartialCollectionView,
            Page = currentPage,
            PageSize = pageSize,
            TotalPages = totalPages,
        };

        if (currentPage > 1)
        {
            view.First = pathGenerator.GenerateFlatCollectionViewFirst(collection, pageSize, orderQueryParam);
            view.Previous =
                pathGenerator.GenerateFlatCollectionViewPrevious(collection, currentPage, pageSize, orderQueryParam);
        }

        if (totalPages > currentPage)
        {
            view.Next = pathGenerator.GenerateFlatCollectionViewNext(collection, currentPage, pageSize, orderQueryParam);
            view.Last = pathGenerator.GenerateFlatCollectionViewLast(collection, totalPages, pageSize, orderQueryParam);
        }

        return view;
    }
    
    private static DescendantCounts? GetDescendantCounts(DbCollection dbAsset, IList<Hierarchy>? items)
    {
        if (!dbAsset.IsStorageCollection) return null;
        if (items.IsNullOrEmpty()) return DescendantCounts.Empty;

        var grouping = items.GroupBy(k => k.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToList();

        return new DescendantCounts(GetCountForType(ResourceType.StorageCollection),
            GetCountForType(ResourceType.IIIFCollection),
            GetCountForType(ResourceType.IIIFManifest));

        int GetCountForType(ResourceType type) => grouping.SingleOrDefault(g => g.Type == type)?.Count ?? 0;
    }
}
