using Models.Database.General;

namespace Repository.Helpers;

public static class HierarchyHelper
{
    /// <summary>
    /// Retrieves the canonical hierarchy from a collection
    /// </summary>
    public static Hierarchy GetCanonical(this IEnumerable<Hierarchy>? hierarchy) =>
        hierarchy?.Single(h => h.Canonical) ?? throw new NullReferenceException("Hierarchy cannot be null");
}
