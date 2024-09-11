using Core.Helpers;
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
            Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}{(string.IsNullOrEmpty(dbAsset.FullPath) ? string.Empty : $"/{dbAsset.FullPath}")}",
            Context = "http://iiif.io/api/presentation/3/context.json",
            Label = dbAsset.Label,
            Items = items != null ? items.Select(x => new Collection()
            {
                Id =  $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{x.FullPath}",
                Label = x.Label
            }).ToList<ICollectionItem>() : new List<ICollectionItem>()
        };

        return collection;
    }

    public static FlatCollection ToFlatCollection(this Models.Database.Collections.Collection dbAsset,
        UrlRoots urlRoots, int pageSize, int currentPage, int totalItems,
        List<Models.Database.Collections.Collection>? items)
    {
        var totalPages = (int)Math.Ceiling(totalItems == 0 ? 1 : (double)totalItems / pageSize);

        return new FlatCollection()
        {
            Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/collections/{dbAsset.Id}",
            Context = new List<string>
            {
                "http://iiif.io/api/presentation/3/context.json",
                "http://tbc.org/iiif-repository/1/context.json"
            },
            Label = dbAsset.Label,
            PublicId =
                $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}{(dbAsset.FullPath != null ? $"/{dbAsset.FullPath}" : "")}",
            Behavior = new List<string>()
                .AppendIf(dbAsset.IsPublic, Behavior.IsPublic)
                .AppendIf(dbAsset.IsStorageCollection, Behavior.IsStorageCollection),
            Type = PresentationType.Collection,
            Slug = dbAsset.Slug,
            Parent = dbAsset.Parent != null
                ? $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/collections/{dbAsset.Parent}"
                : null,

            ItemsOrder = dbAsset.ItemsOrder,
            Items = items != null
                ? items.Select(i => new Item
                {
                    Id = $"{urlRoots.BaseUrl}/{i.CustomerId}/collections/{i.Id}",
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
                Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/collections/{dbAsset.Id}?page=1&pageSize={pageSize}",
                Type = PresentationType.PartialCollectionView,
                Page = currentPage,
                PageSize = pageSize,
                TotalPages = totalPages
            },

            SeeAlso =
            [
                new()
                {
                    Id =
                        $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}{(dbAsset.FullPath != null ? $"/{dbAsset.FullPath}" : "")}",
                    Type = PresentationType.Collection,
                    Label = dbAsset.Label,
                    Profile = ["Public"]
                },

                new()
                {
                    Id =
                        $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}{(dbAsset.FullPath != null ? $"/{dbAsset.FullPath}" : "")}/iiif",
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