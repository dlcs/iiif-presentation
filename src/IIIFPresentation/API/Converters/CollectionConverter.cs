using API.Features.Storage.Models;
using Core.Helpers;
using Core.IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Models.API.Collection;
using Models.Database.General;
using Models.Infrastructure;
using Repository.Paths;
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
        IPathGenerator pathGenerator, string? orderQueryParam = null) =>
        EnrichPresentationCollection(new PresentationCollection(), dbAsset, pageSize, currentPage, totalItems, items,
            parentCollection, pathGenerator, orderQueryParam);

    public static PresentationCollection SetIIIFGeneratedFields(this PresentationCollection iiifCollection, 
        DbCollection dbCollection, IPathGenerator pathGenerator) =>
        EnrichIIIFCollection(iiifCollection, dbCollection, pathGenerator);

    private static PresentationCollection EnrichIIIFCollection(PresentationCollection iiifCollection, 
        DbCollection dbCollection,  IPathGenerator pathGenerator)
    {
        var hierarchy = RetrieveHierarchy(dbCollection);

        iiifCollection.Created = dbCollection.Created.Floor(DateTimeX.Precision.Second);
        iiifCollection.Modified = dbCollection.Modified.Floor(DateTimeX.Precision.Second);
        iiifCollection.CreatedBy = dbCollection.CreatedBy;
        iiifCollection.ModifiedBy = dbCollection.ModifiedBy;
        iiifCollection.PublicId = pathGenerator.GenerateHierarchicalCollectionId(dbCollection);
        iiifCollection.Id = pathGenerator.GenerateFlatId(hierarchy);
        iiifCollection.Parent = GeneratePresentationCollectionParent(pathGenerator, hierarchy);
        iiifCollection.ItemsOrder = hierarchy.ItemsOrder;
        iiifCollection.Tags = dbCollection.Tags;
        iiifCollection.Slug = hierarchy.Slug;

        return iiifCollection;
    }

    /// <summary>
    /// Enriches <see cref="PresentationCollection"/> with values from database.
    /// Note that any values in <see cref="DbCollection"/> "win" and will overwrite those already in
    /// <see cref="PresentationCollection"/>
    /// </summary>
    /// <param name="collection">The presentation collection to enrich</param>
    /// <param name="dbCollection">The database collection to use for enrichment</param>
    /// <param name="pageSize">Size of the page</param>
    /// <param name="currentPage">The current page of items</param>
    /// <param name="totalItems">The total number of items</param>
    /// <param name="items">The list of items that use this collection as a </param>
    /// <param name="parentCollection">The parent collection current collection is part of</param>
    /// <param name="pathGenerator">A collection path generator</param>
    /// <param name="orderQueryParam">Used to describe the type of ordering done</param>
    /// <returns>An enriched presentation collection</returns>
    public static PresentationCollection EnrichPresentationCollection(this PresentationCollection collection,
        DbCollection dbCollection, int pageSize, int currentPage, int totalItems, IList<Hierarchy>? items,
        DbCollection? parentCollection, IPathGenerator pathGenerator, string? orderQueryParam = null)
    {
        items ??= [];
        
        var totalPages = GenerateTotalPages(pageSize, totalItems);

        var orderQueryParamConverted = GenerateOrderQueryParamConverted(orderQueryParam);
        var hierarchy = RetrieveHierarchy(dbCollection);
        
        collection.Id = pathGenerator.GenerateFlatCollectionId(dbCollection);
        collection.Context = GenerateContext();
        collection.FlatId = dbCollection.Id;
        collection.PublicId = pathGenerator.GenerateHierarchicalCollectionId(dbCollection);
        collection.Parent = GeneratePresentationCollectionParent(pathGenerator, hierarchy);
        collection.PartOf = GeneratePartOf(parentCollection, pathGenerator);
        collection.TotalItems = totalItems;
        collection.View = GenerateView(dbCollection, pathGenerator, pageSize, currentPage, totalPages, orderQueryParamConverted);
        collection.SeeAlso = GenerateSeeAlso(dbCollection, pathGenerator);
        collection.Created = dbCollection.Created.Floor(DateTimeX.Precision.Second);
        collection.Modified = dbCollection.Modified.Floor(DateTimeX.Precision.Second);
        collection.CreatedBy = dbCollection.CreatedBy;
        collection.ModifiedBy = dbCollection.ModifiedBy;
        collection.Behavior = GenerateBehavior(dbCollection);
        collection.Slug = hierarchy.Slug;
        collection.ItemsOrder = hierarchy.ItemsOrder;
        collection.Label = dbCollection.Label;
        collection.Totals = GetDescendantCounts(dbCollection, items);
        
        // this is to stop IIIF collection items being overwritten by the hierarchy
        if (dbCollection.IsStorageCollection)
        {
            collection.Items = GenerateItems(pathGenerator, items);
        }

        return collection;
    }
    
    private static Hierarchy RetrieveHierarchy(DbCollection dbCollection) =>
        dbCollection.Hierarchy!.Single(h => h.Canonical);

    private static string GenerateOrderQueryParamConverted(string? orderQueryParam) =>
        string.IsNullOrEmpty(orderQueryParam) ? string.Empty : $"&{orderQueryParam}";

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
    private static int GenerateTotalPages(int pageSize, int totalItems)
    {
        return (int)Math.Ceiling(totalItems == 0 ? 1 : (double)totalItems / pageSize);
    }

    /// <summary>
    /// Generates a parent id for a collection in the presentation collection form
    /// </summary>
    /// <param name="pathGenerator">The path generator</param>
    /// <param name="hierarchy">The hierarchy to get the parent from</param>
    /// <returns>An id of the parent, in the presentation collection form</returns>
    private static string? GeneratePresentationCollectionParent(IPathGenerator pathGenerator, Hierarchy hierarchy)
    {
        return hierarchy.Parent != null
            ? pathGenerator.GenerateFlatParentId(hierarchy)
            : null;
    }

    /// <summary>
    /// Generates a context for a collection
    /// </summary>
    /// <returns>A list of strings</returns>
    private static List<string> GenerateContext()
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
    private static List<string>? GenerateBehavior(DbCollection collection)
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
    private static List<ResourceBase>? GeneratePartOf(DbCollection? collection,
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
    private static List<ExternalResource>? GenerateSeeAlso(DbCollection collection, 
        IPathGenerator pathGenerator)
    {
        if (!collection.IsStorageCollection) return null;
        
        var seeAlso = new List<ExternalResource>();

        if (collection.IsPublic)
        {
            // if the collection is public, include the canonical path
            // NOTE that this will need changed when we implement canonical paths
            AddSeeAlso(Behavior.IsPublic);
        }
        
        // always include hierarchical form
        AddSeeAlso(Behavior.ApiHierarchical);
        
        return seeAlso;

        void AddSeeAlso(string profile) =>
            seeAlso.Add(new ExternalResource(nameof(PresentationType.Collection))
            {
                Id = pathGenerator.GenerateHierarchicalCollectionId(collection),
                Label = collection.Label,
                Profile = profile,
            });
    }

    /// <summary>
    /// Generates items in a hierarchy into the correct format
    /// </summary>
    /// <param name="pathGenerator">Generates a path</param>
    /// <param name="items">The items to convert</param>
    /// <returns>A list of ICollectionItems</returns>
    private static List<ICollectionItem> GenerateItems(IPathGenerator pathGenerator, IEnumerable<Hierarchy> items)
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
    private static View GenerateView(Models.Database.Collections.Collection collection, IPathGenerator pathGenerator, int pageSize,
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
