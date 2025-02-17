﻿using Models.Database;
using Models.Database.Collections;
using Models.Database.General;

namespace Repository.Paths;

public abstract class PathGeneratorBase : IPathGenerator
{
    private const string ManifestsSlug = "manifests";
    private const string CollectionsSlug = "collections";
    private const string CanvasesSlug = "canvases";
    
    /// <summary>
    /// Base url for IIIF Presentation
    /// </summary>
    protected abstract string PresentationUrl { get; }
    
    /// <summary>
    /// Base url for DLCS Protagonist API
    /// </summary>
    protected abstract Uri DlcsApiUrl { get; }
    
    public string GenerateHierarchicalCollectionId(Collection collection) =>
        $"{PresentationUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(collection.FullPath) ? string.Empty : $"/{collection.FullPath}")}";

    public string GenerateHierarchicalFromFullPath(int customerId, string? fullPath) =>
        $"{PresentationUrl}/{customerId}{(fullPath is {Length: > 0} ? $"/{fullPath.TrimStart('/')}" : string.Empty)}";

    public string GenerateHierarchicalCollectionParent(Collection collection, Hierarchy hierarchy)
    {
        var parentPath = collection.FullPath![..^hierarchy.Slug.Length].TrimEnd('/');

        return
            $"{PresentationUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(parentPath) ? string.Empty : $"/{parentPath}")}";
    }

    public string GenerateFlatCollectionId(Collection collection) => 
        $"{PresentationUrl}/{collection.CustomerId}/collections/{collection.Id}";
    
    public string GenerateHierarchicalId(Hierarchy hierarchy) =>
        GenerateHierarchicalFromFullPath(hierarchy.CustomerId, hierarchy.FullPath);
    
    public string GenerateFlatId(Hierarchy hierarchy) =>
        $"{PresentationUrl}/{hierarchy.CustomerId}/{GetSlug(hierarchy.Type)}/{hierarchy.ResourceId}";
    
    public string GenerateFlatParentId(Hierarchy hierarchy) =>
        $"{PresentationUrl}/{hierarchy.CustomerId}/{CollectionsSlug}/{hierarchy.Parent}";
    
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
        $"{PresentationUrl}/{manifest.CustomerId}/{ManifestsSlug}/{manifest.Id}";

    public string GenerateCanvasId(CanvasPainting canvasPainting)
        => $"{PresentationUrl}/{canvasPainting.CustomerId}/{CanvasesSlug}/{canvasPainting.Id}";
    
    public Uri? GenerateSpaceUri(Manifest manifest)
    {
        if (!manifest.SpaceId.HasValue) return null;

        var uriBuilder = new UriBuilder(DlcsApiUrl)
        {
            Path = $"/customers/{manifest.CustomerId}/spaces/{manifest.SpaceId}",
        };
        return uriBuilder.Uri;
    }
    
    public Uri? GenerateAssetUri(CanvasPainting canvasPainting)
    {
        if (canvasPainting.AssetId == null) return null;
        
        var uriBuilder = new UriBuilder(DlcsApiUrl)
        {
            Path = $"/customers/{canvasPainting.AssetId.Customer}/spaces/{canvasPainting.AssetId.Space}/images/{canvasPainting.AssetId.Asset}",
        };
        return uriBuilder.Uri;
    }

    private string GetSlug(ResourceType resourceType) 
        => resourceType == ResourceType.IIIFManifest ? ManifestsSlug : CollectionsSlug;
}
