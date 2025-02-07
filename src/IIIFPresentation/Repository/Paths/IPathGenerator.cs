using Models.Database;
using Models.Database.Collections;
using Models.Database.General;

namespace Repository.Paths;

public interface IPathGenerator
{
    /// <summary>
    /// Get a hierarchical id for the current collection
    /// </summary>
    public string GenerateHierarchicalCollectionId(Collection collection);

    /// <summary>
    /// Get the hierarchical id for the parent of a collection
    /// </summary>
    public string GenerateHierarchicalCollectionParent(Collection collection, Hierarchy hierarchy);

    /// <summary>
    /// Get the flat id for the current collection
    /// </summary>
    public string GenerateFlatCollectionId(Collection collection);

    /// <summary>
    /// Get hierarchical id for current hierarchy item
    /// </summary>
    public string GenerateHierarchicalId(Hierarchy hierarchy);

    /// <summary>
    /// Get flat id for current hierarchy item
    /// </summary>
    public string GenerateFlatId(Hierarchy hierarchy);

    /// <summary>
    /// Get flat id for parent of <see cref="Hierarchy"/> 
    /// </summary>
    public string GenerateFlatParentId(Hierarchy hierarchy);

    /// <summary>
    /// Get the view id for the current collection
    /// </summary>
    public string GenerateFlatCollectionViewId(Collection collection, int currentPage, int pageSize,
        string? orderQueryParam);

    /// <summary>
    /// Get the next id for the current collection view
    /// </summary>
    public Uri GenerateFlatCollectionViewNext(Collection collection, int currentPage, int pageSize,
        string orderQueryParam);

    /// <summary>
    /// Get the previous id for the current collection view
    /// </summary>
    public Uri GenerateFlatCollectionViewPrevious(Collection collection, int currentPage, int pageSize,
        string orderQueryParam);

    /// <summary>
    /// Get the first id for the current collection view
    /// </summary>
    public Uri GenerateFlatCollectionViewFirst(Collection collection, int pageSize, string orderQueryParam);

    /// <summary>
    /// Get the last id for the current collection view
    /// </summary>
    public Uri GenerateFlatCollectionViewLast(Collection collection, int lastPage, int pageSize,
        string orderQueryParam);

    /// <summary>
    /// Get the FullPath of an item, using Canonical slug of attached Hierarchy collection and parent FullPath, if set 
    /// </summary>
    public string GenerateFullPath(Hierarchy collection, Collection parent);

    /// <summary>
    /// Get the FullPath of an item, using Canonical slug of attached Hierarchy collection and provided parent 
    /// </summary>
    public string GenerateFullPath(Hierarchy hierarchy, string? parentPath);

    /// <summary>
    /// Get Id for specified manifest
    /// </summary>
    public string GenerateFlatManifestId(Manifest manifest);
    
    /// <summary>
    /// Get id for specified <see cref="CanvasPainting"/>
    /// </summary>
    public string GenerateCanvasId(CanvasPainting canvasPainting);

    /// <summary>
    /// Get URI for DLCS space for given asset 
    /// </summary>
    Uri? GenerateSpaceUri(Manifest manifest);

    /// <summary>
    /// Get URI of a DLCS asset
    /// </summary>
    Uri? GenerateAssetUri(CanvasPainting canvasPainting);

    string GenerateHierarchicalFromFullPath(int customerId, string? fullPath);
}
