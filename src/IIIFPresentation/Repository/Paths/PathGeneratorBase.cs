using Core.Helpers;
using IIIF.ImageApi;
using Models.Database;
using Models.Database.Collections;
using Models.Database.General;

namespace Repository.Paths;

public abstract class PathGeneratorBase(IPresentationPathGenerator presentationPathGenerator) : IPathGenerator
{
    private const string AnnotationPagesSlug = "annopages";
    private const string PaintingAnnotationSlug = "annotations";
    
    /// <summary>
    /// Base url for DLCS Protagonist API
    /// </summary>
    protected abstract Uri DlcsApiUrl { get; }

    public string GenerateHierarchicalFromFullPath(int customerId, string? fullPath) =>
        presentationPathGenerator.GetHierarchyPresentationPathForRequest(PresentationResourceType.ResourcePublic, 
            customerId, fullPath);

    public string GenerateFlatCollectionId(Collection collection) =>
        presentationPathGenerator.GetFlatPresentationPathForRequest(PresentationResourceType.CollectionPrivate,
            collection.CustomerId, collection.Id);
    
    public string GenerateHierarchicalId(Hierarchy hierarchy) =>
        presentationPathGenerator.GetHierarchyPresentationPathForRequest(PresentationResourceType.ResourcePublic, 
            hierarchy.CustomerId, hierarchy.FullPath);
    
    public string GenerateFlatId(Hierarchy hierarchy) =>
        presentationPathGenerator.GetFlatPresentationPathForRequest(GetResourceType(hierarchy.Type), 
            hierarchy.CustomerId, hierarchy.ResourceId);
    
    public string GenerateFlatParentId(Hierarchy hierarchy) =>
        presentationPathGenerator.GetFlatPresentationPathForRequest(PresentationResourceType.CollectionPrivate,
            hierarchy.CustomerId,
            hierarchy.Parent);
    
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
    
    public string GenerateFullPath(Hierarchy collection, Hierarchy parent)
        => GenerateFullPath(collection, parent.FullPath);
    
    public string GenerateFullPath(Hierarchy hierarchy, string? parentPath) 
        => $"{(!string.IsNullOrEmpty(parentPath) ? $"{parentPath}/" : string.Empty)}{hierarchy.Slug}";
    
    public string GenerateFlatManifestId(Manifest manifest) =>
        presentationPathGenerator.GetFlatPresentationPathForRequest(PresentationResourceType.ManifestPrivate, 
            manifest.CustomerId, manifest.Id);

    public string GenerateCanvasId(CanvasPainting canvasPainting) => 
        presentationPathGenerator.GetFlatPresentationPathForRequest(PresentationResourceType.Canvas, 
            canvasPainting.CustomerId, canvasPainting.Id);

    public string GenerateCanvasIdWithTarget(CanvasPainting canvasPainting)
    {
        var canvasId = GenerateCanvasId(canvasPainting);
        if (string.IsNullOrEmpty(canvasPainting.Target)) return canvasId;
        
        // NOTE(DG) - we only currently only support mediaFragments
        var relevantTarget = Uri.TryCreate(canvasPainting.Target, UriKind.Absolute, out var target)
            ? target.Fragment
            : canvasPainting.Target;
        return string.IsNullOrEmpty(relevantTarget) ? canvasId : canvasId.ToConcatenated('#', relevantTarget);
    }

    public string GenerateAnnotationPagesId(CanvasPainting canvasPainting) => 
        $"{GenerateCanvasId(canvasPainting)}/{AnnotationPagesSlug}/{canvasPainting.CanvasOrder}";

    public string GeneratePaintingAnnotationId(CanvasPainting canvasPainting)
        => $"{GenerateCanvasId(canvasPainting)}/{PaintingAnnotationSlug}/{canvasPainting.CanvasOrder}";

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

    public string? GetModifiedImageRequest(string? existing, int width, int height)
    {
        if (string.IsNullOrEmpty(existing)) return existing;
        if (!ImageRequest.TryParse(existing, out var imageRequest)) return existing;

        imageRequest.Size = new() { Width = width, Height = height };
        return imageRequest.ToString();
    }

    private string GetResourceType(ResourceType resourceType) 
        => resourceType == ResourceType.IIIFManifest ? PresentationResourceType.ManifestPrivate : PresentationResourceType.CollectionPrivate;
}
