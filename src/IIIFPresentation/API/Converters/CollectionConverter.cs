using API.Helpers;
using Core.Helpers;
using Core.IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Models.API.Collection;
using Models.Database.General;
using Models.Infrastucture;

namespace API.Converters;

public static class CollectionConverter
{
    [Obsolete("Use overload that takes Hierarchy")]
    public static Collection ToHierarchicalCollection(this Models.Database.Collections.Collection dbAsset,
        UrlRoots urlRoots, List<Models.Database.Collections.Collection>? items)
    {
        var collection = new Collection()
        {
            Id = dbAsset.GenerateHierarchicalCollectionId(urlRoots),
            Label = dbAsset.Label,
            Items = items?.Count > 0
                ? items.Select(x => new Collection()
                {
                    Id = x.GenerateHierarchicalCollectionId(urlRoots),
                    Label = x.Label
                }).ToList<ICollectionItem>()
                : null
        };

        collection.EnsurePresentation3Context();

        return collection;
    }
    
    public static Collection ToHierarchicalCollection(this Models.Database.Collections.Collection dbAsset,
        UrlRoots urlRoots, List<Models.Database.General.Hierarchy>? items)
    {
        var collection = new Collection()
        {
            Id = dbAsset.GenerateHierarchicalCollectionId(urlRoots),
            Label = dbAsset.Label,
            Items = items?.Count > 0
                ? items.Select(i => GenerateCollectionItem(i, urlRoots, false)).ToList()
                : null
        };

        collection.EnsurePresentation3Context();

        return collection;
    }

    [Obsolete("Use overload that takes Hierarchy")]
    public static PresentationCollection ToFlatCollection(this Models.Database.Collections.Collection dbAsset,
        UrlRoots urlRoots, int pageSize, int currentPage, int totalItems,
        List<Models.Database.Collections.Collection>? items, string? orderQueryParam = null)
    {
        var totalPages = GenerateTotalPages(pageSize, totalItems);

        var orderQueryParamConverted = string.IsNullOrEmpty(orderQueryParam) ? string.Empty : $"&{orderQueryParam}";
        var hierarchy = dbAsset.Hierarchy!.Single(h => h.Canonical);

        return new()
        {
            Id = dbAsset.GenerateFlatCollectionId(urlRoots),
            Context = GenerateContext(),
            Label = dbAsset.Label,
            PublicId = dbAsset.GenerateHierarchicalCollectionId(urlRoots),
            Behavior = GenerateBehavior(dbAsset),
            Slug = hierarchy.Slug,
            Parent = GeneratePresentationCollectionParent(urlRoots, hierarchy),
            ItemsOrder = hierarchy.ItemsOrder,
            Items = GenerateItems(urlRoots, items),
            PartOf = GeneratePartOf(hierarchy, dbAsset, urlRoots),
            TotalItems = totalItems,
            View = GenerateView(dbAsset, urlRoots, pageSize, currentPage, totalPages, orderQueryParamConverted),
            SeeAlso = GenerateSeeAlso(dbAsset, urlRoots),
            Created = dbAsset.Created.Floor(DateTimeX.Precision.Second),
            Modified = dbAsset.Modified.Floor(DateTimeX.Precision.Second),
            CreatedBy = dbAsset.CreatedBy,
            ModifiedBy = dbAsset.ModifiedBy
        };
    }
    
        // NOTE(DG) - this is a duplicate of .ToFlatCollection() that takes list of Collection items.
    // this is a temporary copy as multiple branches are editing this - the only change made here is the type
    // of items and how Items are set
    public static PresentationCollection ToFlatCollection(this Models.Database.Collections.Collection dbAsset,
        UrlRoots urlRoots, int pageSize, int currentPage, int totalItems,
        IEnumerable<Hierarchy>? items, string? orderQueryParam = null)
    {
        var totalPages = (int) Math.Ceiling(totalItems == 0 ? 1 : (double) totalItems / pageSize);
        items ??= [];

        var orderQueryParamConverted = string.IsNullOrEmpty(orderQueryParam) ? string.Empty : $"&{orderQueryParam}";
        var hierarchy = dbAsset.Hierarchy!.Single(h => h.Canonical);

        return new()
        {
            Id = dbAsset.GenerateFlatCollectionId(urlRoots),
            Context = GenerateContext(),
            Label = dbAsset.Label,
            PublicId = dbAsset.GenerateHierarchicalCollectionId(urlRoots),
            Behavior = GenerateBehavior(dbAsset),
            Slug = hierarchy.Slug,
            Parent = GeneratePresentationCollectionParent(urlRoots, hierarchy),
            ItemsOrder = hierarchy.ItemsOrder,
            Items = items.Select(i => GenerateCollectionItem(i, urlRoots, true)).ToList(),
            PartOf = GeneratePartOf(hierarchy, dbAsset, urlRoots),

            TotalItems = totalItems,

            View = GenerateView(dbAsset, urlRoots, pageSize, currentPage, totalPages, orderQueryParamConverted),
            SeeAlso = GenerateSeeAlso(dbAsset, urlRoots),
            Created = dbAsset.Created.Floor(DateTimeX.Precision.Second),
            Modified = dbAsset.Modified.Floor(DateTimeX.Precision.Second),
            CreatedBy = dbAsset.CreatedBy,
            ModifiedBy = dbAsset.ModifiedBy
        };
    }
    
    private static ICollectionItem GenerateCollectionItem(Hierarchy hierarchy, UrlRoots urlRoots, bool flatId)
    {
        var id = flatId ? hierarchy.GenerateFlatId(urlRoots) : hierarchy.GenerateHierarchicalId(urlRoots);
        
        if (hierarchy.Type == ResourceType.IIIFManifest)
        {
            return new Manifest { Id = id };
        }

        return new Collection
        {
            Id = id,
            Label = hierarchy.Collection?.Label
        };
    }

    /// <summary>
    /// Generate the total pages element of a presentation collection
    /// </summary>
    /// <param name="pageSize">The size of the page</param>
    /// <param name="totalItems">The total number of items</param>
    /// <returns>total pages</returns>
    public static int GenerateTotalPages(int pageSize, int totalItems)
    {
        return (int) Math.Ceiling(totalItems == 0 ? 1 : (double) totalItems / pageSize);
    }
    
    /// <summary>
    /// Generates a parent id for a collection in the presentation collection form
    /// </summary>
    /// <param name="urlRoots">The URL</param>
    /// <param name="hierarchy">The hierarchy to get the parent from</param>
    /// <returns>An id of the parent, in the presentation collection form</returns>
    public static string? GeneratePresentationCollectionParent(UrlRoots urlRoots, Hierarchy hierarchy)
    {
        return hierarchy.Parent != null
            ? hierarchy.GenerateFlatParentId(urlRoots)
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
            IIIF.Presentation.Context.Presentation3Context
        ];
    }

    /// <summary>
    /// Generates a list of behaviors for a collection
    /// </summary>
    /// <param name="collection">The dtabase collection to use</param>
    /// <returns>A list of behaviors</returns>
    public static List<string> GenerateBehavior(Models.Database.Collections.Collection collection)
    {
        return new List<string>()
            .AppendIf(collection.IsPublic, Behavior.IsPublic)
            .AppendIf(collection.IsStorageCollection, Behavior.IsStorageCollection);
    }

    /// <summary>
    /// Generates the PartOf field for a collection
    /// </summary>
    /// <param name="hierarchy">The hierarchy to use to generate</param>
    /// <param name="collection">The collection required</param>
    /// <param name="urlRoots">The URL</param>
    /// <returns>A list of ResourceBase</returns>
    public static List<ResourceBase>? GeneratePartOf(Hierarchy hierarchy, Models.Database.Collections.Collection collection, UrlRoots urlRoots)
    {
        return hierarchy.Parent != null
            ? new List<ResourceBase>
            {
                new PartOf(nameof(PresentationType.Collection))
                {
                    Id = $"{urlRoots.BaseUrl}/{collection.CustomerId}/{hierarchy.Parent}",
                    Label = collection.Label
                }
            }
            : null;
    }

    /// <summary>
    /// Generates the SeeAlso part of a collection
    /// </summary>
    /// <param name="collection">The collection to use in generation</param>
    /// <param name="urlRoots">The URL</param>
    /// <returns>A list of external resources</returns>
    public static List<ExternalResource>? GenerateSeeAlso(Models.Database.Collections.Collection collection, UrlRoots urlRoots)
    {
        return [
            new(nameof(PresentationType.Collection))
            {
                Id = collection.GenerateHierarchicalCollectionId(urlRoots),
                Label = collection.Label,
                Profile = "Public"
            },

            new(nameof(PresentationType.Collection))
            {
                Id = $"{collection.GenerateHierarchicalCollectionId(urlRoots)}/iiif",
                Label = collection.Label,
                Profile = "api-hierarchical"
            }
        ];
    }

    /// <summary>
    /// Generates items in a collection into the correct format
    /// </summary>
    /// <param name="urlRoots">The URL to use</param>
    /// <param name="items">The items to convert</param>
    /// <returns>A list of ICollectionItems</returns>
    public static List<ICollectionItem> GenerateItems(UrlRoots urlRoots, List<Models.Database.Collections.Collection>? items)
    {
        return items != null
            ? items.Select(i => (ICollectionItem) new Collection()
            {
                Id = i.GenerateFlatCollectionId(urlRoots),
                Label = i.Label
            }).ToList()
            : [];
    }

    /// <summary>
    /// Generates the view component of a presentation collection
    /// </summary>
    /// <param name="collection">The database collection to use in the convewrsion</param>
    /// <param name="urlRoots">The URL</param>
    /// <param name="pageSize">Sets the page size</param>
    /// <param name="currentPage">The current page that the view is being generated for</param>
    /// <param name="totalPages">How many pages to generate the View for</param>
    /// <param name="orderQueryParam">What the View is being ordered by</param>
    /// <returns>A View</returns>
    public static View GenerateView(Models.Database.Collections.Collection collection, UrlRoots urlRoots, int pageSize,
        int currentPage, int totalPages, string? orderQueryParam = null)
    {
        var view = new View()
        {
            Id = collection.GenerateFlatCollectionViewId(urlRoots, currentPage, pageSize, orderQueryParam),
            Type = PresentationType.PartialCollectionView,
            Page = currentPage,
            PageSize = pageSize,
            TotalPages = totalPages,
        };

        if (currentPage > 1)
        {
            view.First = collection.GenerateFlatCollectionViewFirst(urlRoots, pageSize, orderQueryParam);
            view.Previous =
                collection.GenerateFlatCollectionViewPrevious(urlRoots, currentPage, pageSize, orderQueryParam);
        }

        if (totalPages > currentPage)
        {
            view.Next = collection.GenerateFlatCollectionViewNext(urlRoots, currentPage, pageSize, orderQueryParam);
            view.Last = collection.GenerateFlatCollectionViewLast(urlRoots, totalPages, pageSize, orderQueryParam);
        }

        return view;
    }
}