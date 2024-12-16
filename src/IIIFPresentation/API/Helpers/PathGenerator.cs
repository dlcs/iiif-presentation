using API.Infrastructure.Requests;
using DLCS;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Models.Database.General;
using CanvasPainting = Models.Database.CanvasPainting;

namespace API.Helpers;

public class PathGenerator : IPathGenerator
{
    private const string ManifestsSlug = "manifests";
    private const string CollectionsSlug = "collections";
    private const string CanvasesSlug = "canvases";
    private readonly string baseUrl;
    private readonly DlcsSettings dlcsSettings;

    public PathGenerator(IHttpContextAccessor contextAccessor, IOptions<DlcsSettings> dlcsOptions)
    {
         baseUrl = contextAccessor.HttpContext!.Request.GetBaseUrl();
         dlcsSettings = dlcsOptions.Value;
    }
    
    public string GenerateHierarchicalCollectionId(Collection collection) =>
        $"{baseUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(collection.FullPath) ? string.Empty : $"/{collection.FullPath}")}";

    public string GenerateHierarchicalCollectionParent(Collection collection, Hierarchy hierarchy)
    {
        var parentPath = collection.FullPath![..^hierarchy.Slug.Length].TrimEnd('/');

        return
            $"{baseUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(parentPath) ? string.Empty : $"/{parentPath}")}";
    }

    public string GenerateFlatCollectionId(Collection collection) => 
        $"{baseUrl}/{collection.CustomerId}/collections/{collection.Id}";
    
    public string GenerateHierarchicalId(Hierarchy hierarchy) =>
        $"{baseUrl}/{hierarchy.CustomerId}{(string.IsNullOrEmpty(hierarchy.FullPath) ? string.Empty : $"/{hierarchy.FullPath}")}";
    
    public string GenerateFlatId(Hierarchy hierarchy) =>
        $"{baseUrl}/{hierarchy.CustomerId}/{GetSlug(hierarchy.Type)}/{hierarchy.ResourceId}";
    
    public string GenerateFlatParentId(Hierarchy hierarchy) =>
        $"{baseUrl}/{hierarchy.CustomerId}/{CollectionsSlug}/{hierarchy.Parent}";
    
    public string GenerateFlatCollectionViewId(Collection collection, int currentPage, int pageSize, 
        string? orderQueryParam) =>
        $"{GenerateFlatCollectionId(collection)}?page={currentPage}&pageSize={pageSize}{orderQueryParam}";
    
    public Uri GenerateFlatCollectionViewNext(Collection collection, int currentPage, int pageSize, 
        string orderQueryParam) =>
        new(
            $"{GenerateFlatCollectionId(collection)}?page={currentPage + 1}&pageSize={pageSize}{orderQueryParam}");
    
    public Uri GenerateFlatCollectionViewPrevious(Collection collection,int currentPage, int pageSize, 
        string orderQueryParam) =>
        new(
            $"{GenerateFlatCollectionId(collection)}?page={currentPage - 1}&pageSize={pageSize}{orderQueryParam}");
    
    public Uri GenerateFlatCollectionViewFirst(Collection collection, int pageSize, string orderQueryParam) =>
        new(
            $"{GenerateFlatCollectionId(collection)}?page=1&pageSize={pageSize}{orderQueryParam}");
    
    public Uri GenerateFlatCollectionViewLast(Collection collection, int lastPage, int pageSize, 
        string orderQueryParam) => new(
            $"{GenerateFlatCollectionId(collection)}?page={lastPage}&pageSize={pageSize}{orderQueryParam}");
    
    public string GenerateFullPath(Hierarchy collection, Collection parent)
        => GenerateFullPath(collection, parent.FullPath);
    
    public string GenerateFullPath(Hierarchy hierarchy, string? parentPath) 
        => $"{(!string.IsNullOrEmpty(parentPath) ? $"{parentPath}/" : string.Empty)}{hierarchy.Slug}";
    
    public string GenerateFlatManifestId(Manifest manifest) =>
        $"{baseUrl}/{manifest.CustomerId}/{ManifestsSlug}/{manifest.Id}";

    public string GenerateCanvasId(CanvasPainting canvasPainting)
        => $"{baseUrl}/{canvasPainting.CustomerId}/{CanvasesSlug}/{canvasPainting.Id}";
    
    public Uri? GenerateSpaceUri(Manifest manifest)
    {
        if (!manifest.SpaceId.HasValue) return null;

        var uriBuilder = new UriBuilder(dlcsSettings.ApiUri)
        {
            Path = $"/customers/{manifest.CustomerId}/spaces/{manifest.SpaceId}",
        };
        return uriBuilder.Uri;
    }
    
    public string? GenerateAssetUri(CanvasPainting canvasPainting)
    {
        if (string.IsNullOrEmpty(canvasPainting.AssetId)) return null;
        var assetId = canvasPainting.AssetId.Split('/');
        if (assetId.Length != 3) return null;

        var uriBuilder = new UriBuilder(dlcsSettings.ApiUri)
        {
            Path = $"/customers/{assetId[0]}/spaces/{assetId[1]}/images/{assetId[2]}",
        };
        return uriBuilder.Uri.ToString();
    }

    private string GetSlug(ResourceType resourceType) 
        => resourceType == ResourceType.IIIFManifest ? ManifestsSlug : CollectionsSlug;
}
