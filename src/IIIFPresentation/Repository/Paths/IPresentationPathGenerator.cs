namespace Repository.Paths;

public interface IPresentationPathGenerator
{
    /// <summary>
    /// Generate full path for IIIF Hierarchical  Presentation resources
    /// </summary>
    /// <param name="created">
    /// Created date of the resource the path is being generated for, left `null` if not known yet
    /// </param>
    public string GetHierarchyPresentationPathForRequest(string presentationServiceType, int customerId,
        string hierarchyPath, DateTime? created = null);

    /// <summary>
    /// Generate full path for IIIF Presentation resources
    /// </summary>
    /// <param name="created">
    /// Created date of the resource the path is being generated for, left `null` if not known yet
    /// </param>
    public string GetFlatPresentationPathForRequest(string presentationServiceType, int customerId, string resourceId,
        DateTime? created = null);
}
