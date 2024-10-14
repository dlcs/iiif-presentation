using API.Features.Storage.Models;
using API.Helpers;
using Core.Helpers;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using Models.API.Collection;
using Models.Infrastucture;

namespace API.Converters;

public static class CollectionConverter
{
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

    public static PresentationCollection ToFlatCollection(this HierarchicalCollection dbAsset,
        UrlRoots urlRoots, int pageSize, int currentPage, int totalItems,
        List<Models.Database.Collections.Collection>? items, string? orderQueryParam = null)
    {
        var totalPages = (int) Math.Ceiling(totalItems == 0 ? 1 : (double) totalItems / pageSize);

        var orderQueryParamConverted = string.IsNullOrEmpty(orderQueryParam) ? string.Empty : $"&{orderQueryParam}";

        return new()
        {
            Id = dbAsset.Collection.GenerateFlatCollectionId(urlRoots),
            Context = new List<string>
            {
                "http://tbc.org/iiif-repository/1/context.json",
                "http://iiif.io/api/presentation/3/context.json"
            },
            Label = dbAsset.Collection.Label,
            PublicId = dbAsset.Collection.GenerateHierarchicalCollectionId(urlRoots),
            Behavior = new List<string>()
                .AppendIf(dbAsset.Collection.IsPublic, Behavior.IsPublic)
                .AppendIf(dbAsset.Collection.IsStorageCollection, Behavior.IsStorageCollection),
            Slug = dbAsset.Collection.Slug,
            Parent = dbAsset.Hierarchy.Parent != null
                ? dbAsset.Hierarchy.GenerateFlatCollectionParent(urlRoots)
                : null,

            ItemsOrder = dbAsset.Hierarchy.ItemsOrder,
            Items = items != null
                ? items.Select(i => (ICollectionItem) new Collection()
                {
                    Id = i.GenerateFlatCollectionId(urlRoots),
                    Label = i.Label
                }).ToList()
                : [],

            PartOf = dbAsset.Hierarchy.Parent != null
                ?
                [
                    new PartOf(nameof(PresentationType.Collection))
                    {
                        Id = $"{urlRoots.BaseUrl}/{dbAsset.Collection.CustomerId}/{dbAsset.Hierarchy.Parent}",
                        Label = dbAsset.Collection.Label
                    }
                ]
                : null,

            TotalItems = totalItems,

            View = GenerateView(dbAsset.Collection, urlRoots, pageSize, currentPage, totalPages, orderQueryParamConverted),

            SeeAlso =
            [
                new(nameof(PresentationType.Collection))
                {
                    Id = dbAsset.Collection.GenerateHierarchicalCollectionId(urlRoots),
                    Label = dbAsset.Collection.Label,
                    Profile = "Public"
                },

                new(nameof(PresentationType.Collection))
                {
                    Id = $"{dbAsset.Collection.GenerateHierarchicalCollectionId(urlRoots)}/iiif",
                    Label = dbAsset.Collection.Label,
                    Profile = "api-hierarchical"
                }
            ],

            Created = dbAsset.Collection.Created.Floor(DateTimeX.Precision.Second),
            Modified = dbAsset.Collection.Modified.Floor(DateTimeX.Precision.Second),
            CreatedBy = dbAsset.Collection.CreatedBy,
            ModifiedBy = dbAsset.Collection.ModifiedBy
        };
    }

    private static View GenerateView(Models.Database.Collections.Collection dbAsset, UrlRoots urlRoots, int pageSize,
        int currentPage, int totalPages, string? orderQueryParam = null)

    {
        var view = new View()
        {
            Id = dbAsset.GenerateFlatCollectionViewId(urlRoots, currentPage, pageSize, orderQueryParam),
            Type = PresentationType.PartialCollectionView,
            Page = currentPage,
            PageSize = pageSize,
            TotalPages = totalPages,
        };

        if (currentPage > 1)
        {
            view.First = dbAsset.GenerateFlatCollectionViewFirst(urlRoots, pageSize, orderQueryParam);
            view.Previous =
                dbAsset.GenerateFlatCollectionViewPrevious(urlRoots, currentPage, pageSize, orderQueryParam);
        }

        if (totalPages > currentPage)
        {
            view.Next = dbAsset.GenerateFlatCollectionViewNext(urlRoots, currentPage, pageSize, orderQueryParam);
            view.Last = dbAsset.GenerateFlatCollectionViewLast(urlRoots, totalPages, pageSize, orderQueryParam);
        }

        return view;
    }
}