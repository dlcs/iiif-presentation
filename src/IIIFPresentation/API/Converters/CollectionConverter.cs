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

    public static PresentationCollection ToFlatCollection(this Models.Database.Collections.Collection dbAsset,
        UrlRoots urlRoots, int pageSize, int currentPage, int totalItems,
        List<Models.Database.Collections.Collection>? items, string? orderQueryParam = null)
    {
        var totalPages = (int) Math.Ceiling(totalItems == 0 ? 1 : (double) totalItems / pageSize);

        var orderQueryParamConverted = string.IsNullOrEmpty(orderQueryParam) ? string.Empty : $"&{orderQueryParam}";
        var hierarchy = dbAsset.Hierarchy!.Single(h => h.Canonical);

        return new()
        {
            Id = dbAsset.GenerateFlatCollectionId(urlRoots),
            Context = new List<string>
            {
                "http://tbc.org/iiif-repository/1/context.json",
                "http://iiif.io/api/presentation/3/context.json"
            },
            Label = dbAsset.Label,
            PublicId = dbAsset.GenerateHierarchicalCollectionId(urlRoots),
            Behavior = new List<string>()
                .AppendIf(dbAsset.IsPublic, Behavior.IsPublic)
                .AppendIf(dbAsset.IsStorageCollection, Behavior.IsStorageCollection),
            Slug = hierarchy.Slug,
            Parent = hierarchy.Parent != null
                ? hierarchy.GenerateFlatCollectionParent(urlRoots)
                : null,

            ItemsOrder = hierarchy.ItemsOrder,
            Items = items != null
                ? items.Select(i => (ICollectionItem) new Collection()
                {
                    Id = i.GenerateFlatCollectionId(urlRoots),
                    Label = i.Label
                }).ToList()
                : [],

            PartOf = hierarchy.Parent != null
                ?
                [
                    new PartOf(nameof(PresentationType.Collection))
                    {
                        Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{hierarchy.Parent}",
                        Label = dbAsset.Label
                    }
                ]
                : null,

            TotalItems = totalItems,

            View = GenerateView(dbAsset, urlRoots, pageSize, currentPage, totalPages, orderQueryParamConverted),

            SeeAlso =
            [
                new(nameof(PresentationType.Collection))
                {
                    Id = dbAsset.GenerateHierarchicalCollectionId(urlRoots),
                    Label = dbAsset.Label,
                    Profile = "Public"
                },

                new(nameof(PresentationType.Collection))
                {
                    Id = $"{dbAsset.GenerateHierarchicalCollectionId(urlRoots)}/iiif",
                    Label = dbAsset.Label,
                    Profile = "api-hierarchical"
                }
            ],

            Created = dbAsset.Created.Floor(DateTimeX.Precision.Second),
            Modified = dbAsset.Modified.Floor(DateTimeX.Precision.Second),
            CreatedBy = dbAsset.CreatedBy,
            ModifiedBy = dbAsset.ModifiedBy
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