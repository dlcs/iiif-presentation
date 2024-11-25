using API.Converters;
using Models.Database.Collections;
using Models.Database.General;

namespace API.Helpers;

public interface IPathGenerator
{
    public string GenerateHierarchicalCollectionId(Collection collection);

    public string GenerateHierarchicalCollectionParent(Collection collection, Hierarchy hierarchy);

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

    public string GenerateFlatCollectionViewId(Collection collection, int currentPage, int pageSize,
        string? orderQueryParam);

    public Uri GenerateFlatCollectionViewNext(Collection collection, int currentPage, int pageSize,
        string orderQueryParam);

    public Uri GenerateFlatCollectionViewPrevious(Collection collection, int currentPage, int pageSize,
        string orderQueryParam);

    public Uri GenerateFlatCollectionViewFirst(Collection collection, int pageSize, string orderQueryParam);

    public Uri GenerateFlatCollectionViewLast(Collection collection, int lastPage, int pageSize,
        string orderQueryParam);

    /// <summary>
    /// Get the FullPath of an item, using Canonical slug of attached Hierarchy collection and parent FullPath, if set 
    /// </summary>
    public string GenerateFullPath(Hierarchy collection, Collection parent);

    /// <summary>
    /// Get the FullPath of an item, using Canonical slug of attached Hierarcy collection and provided parent 
    /// </summary>
    public string GenerateFullPath(Hierarchy hierarchy, string? parentPath);

    /// <summary>
    /// Get Id for specified manifest
    /// </summary>
    public string GenerateFlatManifestId(Manifest manifest);
}