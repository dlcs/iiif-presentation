namespace Repository.Paths;

public interface IPresentationPathGenerator
{
    /// <summary>
    /// Generate full path for IIIF Presentation resources
    /// </summary>
    public string GetPresentationPathForRequest(string presentationServiceType, int customerId,
        string? hierarchyPath = null, string? resourceId = null);
}
