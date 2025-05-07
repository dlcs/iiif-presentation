namespace Repository.Paths;

public interface IPresentationPathGenerator
{
    /// <summary>
    /// Generate full path for IIIF Hierarchical  Presentation resources
    /// </summary>
    public string GetHierarchyPresentationPathForRequest(string presentationServiceType, int customerId,
        string hierarchyPath);
    
    /// <summary>
    /// Generate full path for IIIF Presentation resources
    /// </summary>
    public string GetFlatPresentationPathForRequest(string presentationServiceType, int customerId, string resourceId);
}
