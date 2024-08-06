using Models.Database.Collections;
using Models.Response;

namespace API.Converters;

public static class CollectionConverter
{
    public static HierarchicalCollection ToHierarchicalCollection(this Collection dbAsset, UrlRoots urlRoots)
    {
        return new HierarchicalCollection()
        {
            Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Slug}",
            Context = "http://iiif.io/api/presentation/3/context.json",
            Label = dbAsset.Label,
            Items = new List<Item>(),
            Type = "Collection"
        };
    }
    
    public static FlatCollection ToFlatCollection(this Collection dbAsset, UrlRoots urlRoots, int pageSize, List<Item> items)
    {
        return new FlatCollection()
        {
            Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Id}",
            Context = new List<string> 
                {
                    "http://iiif.io/api/presentation/3/context.json", 
                    "http://tbc.org/iiif-repository/1/context.json" 
                },
            Label = dbAsset.Label,
            Type = "Collection",
            Slug = dbAsset.Slug,
            Parent = dbAsset.Parent,
            
            ItemsOrder = dbAsset.ItemsOrder,
            Items = new List<Item>(),
            
            PartOf = dbAsset.Parent != null ? new List<PartOf>() 
            { new()
                {
                    Id = dbAsset.Parent,
                    Label = dbAsset.Label, // TODO: modify to parent
                    Type = "Collection"
                }
            } : null,
            
            TotalItems = items.Count,
            
            View = new View()
            {
                Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Slug}?page=1&pageSize={pageSize}",
                Type = "PartialCollectionView",
                Page = 1,
                PageSize = pageSize,
                TotalPages = items.Count % pageSize + 1
            },
            
            SeeAlso = new List<SeeAlso>()
            {
                new()
                {
                    Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}",
                    Type = "Collection",
                    Label = dbAsset.Label,
                    Profile = new List<string>()
                    {
                        "Public"
                    }
                },
                new()
                {
                    Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/iiif",
                    Type = "Collection",
                    Label = dbAsset.Label,
                    Profile = new List<string>()
                    {
                        "api-hierarchical"
                    }
                }
            },
            
            Created = dbAsset.Created,
            Modified = dbAsset.Modified,
            CreatedBy = dbAsset.CreatedBy,
            ModifiedBy = dbAsset.ModifiedBy
        };
    }
}