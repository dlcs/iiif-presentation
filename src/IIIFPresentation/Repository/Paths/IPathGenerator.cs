using Models.Database;
using Models.Database.Collections;
using Models.Database.General;

namespace Repository.Paths;

public interface IPathGenerator
{
    /// <summary>
    /// Get the flat id for the current collection
    /// </summary>
    string GenerateFlatCollectionId(Collection collection);

    /// <summary>
    /// Get hierarchical id for current hierarchy item
    /// </summary>
    string GenerateHierarchicalId(Hierarchy hierarchy);

    /// <summary>
    /// Get flat id for current hierarchy item
    /// </summary>
    string GenerateFlatId(Hierarchy hierarchy);

    /// <summary>
    /// Get flat id for parent of <see cref="Hierarchy"/> 
    /// </summary>
    string GenerateFlatParentId(Hierarchy hierarchy);
    
    /// <summary>
    /// Get the view id for the current collection
    /// </summary>
    string GenerateFlatCollectionViewId(Collection collection, int currentPage, int pageSize,
        string? orderQueryParam);

    /// <summary>
    /// Get the next id for the current collection view
    /// </summary>
    Uri GenerateFlatCollectionViewNext(Collection collection, int currentPage, int pageSize,
        string orderQueryParam);

    /// <summary>
    /// Get the previous id for the current collection view
    /// </summary>
    Uri GenerateFlatCollectionViewPrevious(Collection collection, int currentPage, int pageSize,
        string orderQueryParam);

    /// <summary>
    /// Get the first id for the current collection view
    /// </summary>
    Uri GenerateFlatCollectionViewFirst(Collection collection, int pageSize, string orderQueryParam);

    /// <summary>
    /// Get the last id for the current collection view
    /// </summary>
    Uri GenerateFlatCollectionViewLast(Collection collection, int lastPage, int pageSize,
        string orderQueryParam);

    /// <summary>
    /// Get the FullPath of an item, using Canonical slug of attached Hierarchy collection and parent FullPath, if set 
    /// </summary>
    string GenerateFullPath(Hierarchy collection, Hierarchy parent);

    /// <summary>
    /// Get the FullPath of an item, using Canonical slug of attached Hierarchy collection and provided parent 
    /// </summary>
    string GenerateFullPath(Hierarchy hierarchy, string? parentPath);

    /// <summary>
    /// Get Id for specified manifest
    /// </summary>
    string GenerateFlatManifestId(Manifest manifest);
    
    /// <summary>
    /// Get canvas id for specified <see cref="CanvasPainting"/>
    /// </summary>
    string GenerateCanvasId(CanvasPainting canvasPainting);
    
    /// <summary>
    /// Get AnnotationPage id for specified <see cref="CanvasPainting"/>
    /// </summary>
    string GenerateAnnotationPagesId(CanvasPainting canvasPainting);
    
    /// <summary>
    /// Get PaintingAnnotation id for specified <see cref="CanvasPainting"/>
    /// </summary>
    string GeneratePaintingAnnotationId(CanvasPainting canvasPainting);

    /// <summary>
    /// Get URI for DLCS space for given asset 
    /// </summary>
    Uri? GenerateSpaceUri(Manifest manifest);

    /// <summary>
    /// Get URI of a DLCS asset
    /// </summary>
    Uri? GenerateAssetUri(CanvasPainting canvasPainting);

    /// <summary>
    /// Generate the hierarchical id for specified customer and path slugs 
    /// </summary>
    string GenerateHierarchicalFromFullPath(int customerId, string? fullPath);

    /// <summary>
    ///     Parses an image request URI and rewrites it using provided width and height values
    /// </summary>
    /// <param name="existing">Existing image request uri</param>
    /// <param name="width">new width to use</param>
    /// <param name="height">new height to use</param>
    /// <returns></returns>
    string? GetModifiedImageRequest(string? existing, int width, int height);
}
