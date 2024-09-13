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
            Items = items != null ? items.Select(x => new Collection()
            {
                Id =  x.GenerateHierarchicalCollectionId(urlRoots),
                Label = x.Label
            }).ToList<ICollectionItem>() : []
        };
        
        collection.EnsurePresentation3Context();

        return collection;
    }

    public static FlatCollection ToFlatCollection(this Models.Database.Collections.Collection dbAsset,
        UrlRoots urlRoots, int pageSize, int currentPage, int totalItems,
        List<Models.Database.Collections.Collection>? items, string? orderQueryParam = null)
    {
        var totalPages = (int)Math.Ceiling(totalItems == 0 ? 1 : (double)totalItems / pageSize);

        var orderQueryParamConverted = string.IsNullOrEmpty(orderQueryParam) ? string.Empty : $"&{orderQueryParam}";
        
        return new FlatCollection()
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
            Type = PresentationType.Collection,
            Slug = dbAsset.Slug,
            Parent = dbAsset.Parent != null
                ? dbAsset.GenerateFlatCollectionParent(urlRoots)
                : null,

            ItemsOrder = dbAsset.ItemsOrder,
            Items = items != null
                ? items.Select(i => new Item
                {
                    Id = i.GenerateFlatCollectionId(urlRoots),
                    Label = i.Label,
                    Type = PresentationType.Collection
                }).ToList()
                : [],

            PartOf = dbAsset.Parent != null
                ? new List<PartOf>
                {
                    new()
                    {
                        Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Parent}",
                        Label = dbAsset.Label,
                        Type = PresentationType.Collection
                    }
                }
                : null,

            TotalItems = totalItems,

            View = new View
            {
                Id = dbAsset.GenerateFlatCollectionViewId(urlRoots, currentPage, pageSize, orderQueryParamConverted),
                Type = PresentationType.PartialCollectionView,
                Page = currentPage,
                PageSize = pageSize,
                TotalPages = totalPages,
                Next = totalPages > currentPage
                    ? dbAsset.GenerateFlatCollectionViewNext(urlRoots, currentPage, pageSize, orderQueryParamConverted)
                    : null,
                Last = currentPage > 1 // check if we're on a page after the first
                    ? dbAsset.GenerateFlatCollectionViewLast(urlRoots, currentPage, pageSize, orderQueryParamConverted)
                    : null
            },

            SeeAlso =
            [
                new()
                {
                    Id = dbAsset.GenerateHierarchicalCollectionId(urlRoots),
                    Type = PresentationType.Collection,
                    Label = dbAsset.Label,
                    Profile = ["Public"]
                },

                new()
                {
                    Id = $"{dbAsset.GenerateHierarchicalCollectionId(urlRoots)}/iiif",
                    Type = PresentationType.Collection,
                    Label = dbAsset.Label,
                    Profile = ["api-hierarchical"]
                }
            ],

            Created = dbAsset.Created,
            Modified = dbAsset.Modified,
            CreatedBy = dbAsset.CreatedBy,
            ModifiedBy = dbAsset.ModifiedBy
        };
    }
}