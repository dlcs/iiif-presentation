namespace Models;

public static class KnownCollections
{
    /// <summary>
    /// The id of root collection
    /// </summary>
    public const string RootCollection = "root";

    /// <summary>
    /// Check if specified id is for root collection 
    /// </summary>
    public static bool IsRoot(string collectionId) =>
        string.Equals(collectionId, RootCollection, StringComparison.OrdinalIgnoreCase);
}
