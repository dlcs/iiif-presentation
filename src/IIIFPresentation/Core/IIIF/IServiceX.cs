using IIIF;

namespace Core.IIIF;

public static class IServiceX
{
    /// <summary>
    /// Get a list of unique ids.
    /// </summary>
    public static HashSet<string> GetDistinctIds<T>(this IList<T> target) where T : IService
        => [..target.Select(s => s.Id).Where(id => id != null)!];
}
