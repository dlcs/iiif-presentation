namespace Models.API.Collection;

/// <summary>
/// Represents counts of descendant properties by type
/// </summary>
public record DescendantCounts(int ChildStorageCollections, int ChildIIIFCollections, int ChildManifests)
{
    /// <summary>
    /// Empty <see cref="DescendantCounts"/> object
    /// </summary>
    public static readonly DescendantCounts Empty = new(0, 0, 0);
}
