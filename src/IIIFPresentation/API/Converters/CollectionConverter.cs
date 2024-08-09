using Models.Database.Collections;
using Models.Response;

namespace API.Converters;

public static class CollectionConverter
{
    public static HierarchicalCollection ToHierarchicalCollection(this Collection dbAsset, UrlRoots urlRoots,
        IQueryable<Collection>? items)
    {
        return new HierarchicalCollection()
        {
            Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Slug}",
            Context = "http://iiif.io/api/presentation/3/context.json",
            Label = dbAsset.Label,
            Items = items != null ? items.Select(x => new Item
            {
                Id =  $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Id}",
                Label = x.Label,
                Type = PresentationType.Collection
            }).ToList() : new List<Item>(),
            Type = PresentationType.Collection
        };
    }
    
    public static FlatCollection ToFlatCollection(this Collection dbAsset, UrlRoots urlRoots, int pageSize, 
        IQueryable<Collection>? items)
    {
        var itemCount = items?.Count() ?? 0;
        
        return new FlatCollection()
        {
            Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.FullPath ?? dbAsset.Id}",
            Context = new List<string> 
                {
                    "http://iiif.io/api/presentation/3/context.json", 
                    "http://tbc.org/iiif-repository/1/context.json" 
                },
            Label = dbAsset.Label,
            Type = PresentationType.Collection,
            Slug = dbAsset.Slug,
            Parent = dbAsset.Parent,
            
            ItemsOrder = dbAsset.ItemsOrder,
            Items = items != null ? items.Select(i => new Item
            {
                Id =  $"{urlRoots.BaseUrl}/{i.FullPath ?? i.Slug}",
                Label = i.Label,
                Type = PresentationType.Collection
            }).ToList() : new List<Item>(),
            
            PartOf = dbAsset.Parent != null ? new List<PartOf>() 
            { 
                new()
                {
                    Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Parent}",
                    Label = dbAsset.Label,
                    Type = PresentationType.Collection
                }
            } : null,
            
            TotalItems = itemCount,
            
            View = new View()
            {
                Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/collections/{dbAsset.Id}?page=1&pageSize={pageSize}",
                Type = PresentationType.PartialCollectionView,
                Page = 1,
                PageSize = pageSize,
                TotalPages = itemCount % pageSize + 1
            },
            
            SeeAlso = new List<SeeAlso>()
            {
                new()
                {
                    Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Id}",
                    Type = PresentationType.Collection,
                    Label = dbAsset.Label,
                    Profile = new List<string>()
                    {
                        "Public"
                    }
                },
                new()
                {
                    Id = $"{urlRoots.BaseUrl}/{dbAsset.CustomerId}/{dbAsset.Id}/iiif",
                    Type = PresentationType.Collection,
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