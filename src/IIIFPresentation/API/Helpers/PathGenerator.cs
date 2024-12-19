using API.Infrastructure.Requests;
using DLCS;
using DLCS.Models;
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

    public string GenerateHierarchicalFromFullPath(int customerId, string? fullPath) =>
        $"{baseUrl}/{customerId}{(fullPath is {Length: > 0} ? $"/{fullPath}" : string.Empty)}";

    public string GenerateHierarchicalCollectionParent(Collection collection, Hierarchy hierarchy)
    {
        var parentPath = collection.FullPath![..^hierarchy.Slug.Length].TrimEnd('/');

        return
            $"{baseUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(parentPath) ? string.Empty : $"/{parentPath}")}";
    }

    public string GenerateFlatCollectionId(Collection collection) => 
        $"{baseUrl}/{collection.CustomerId}/collections/{collection.Id}";
    
    public string GenerateHierarchicalId(Hierarchy hierarchy) =>
        GenerateHierarchicalFromFullPath(hierarchy.CustomerId, hierarchy.FullPath);
    
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
    
    public Uri? GenerateAssetUri(CanvasPainting canvasPainting)
    {
        if (string.IsNullOrEmpty(canvasPainting.AssetId)) return null;

        AssetId assetId;

        try
        {
            assetId = AssetId.FromString(canvasPainting.AssetId);
        }
        catch // swallow error as it's not needed
        {
            return null;
        }

        var uriBuilder = new UriBuilder(dlcsSettings.ApiUri)
        {
            Path = $"/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}",
        };
        return uriBuilder.Uri;
    }

    private string GetSlug(ResourceType resourceType) 
        => resourceType == ResourceType.IIIFManifest ? ManifestsSlug : CollectionsSlug;
}
