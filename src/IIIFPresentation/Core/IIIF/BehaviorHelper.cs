using Core.Helpers;
using Core.Infrastructure;
using IIIF.Presentation.V3;

namespace Core.IIIF;

public static class BehaviorHelper
{
    private static readonly string[] PresentationBehaviours = [Behavior.IsStorageCollection, Behavior.IsPublic];

    /// <summary>
    /// Whether the behaviors mark a resource as publicly available
    /// </summary>
    public static bool IsPublic(this List<string>? behaviors) => behaviors?.Contains(Behavior.IsPublic) ?? false;

    /// <summary>
    /// Whether the behaviors mark a resource as a storage collection
    /// </summary>
    public static bool IsStorageCollection(this List<string>? behaviors)
        => behaviors?.Contains(Behavior.IsStorageCollection) ?? false;

    public static void RemovePresentationBehaviours(this ResourceBase iiifResource)
    {
        if (iiifResource.Behavior.IsNullOrEmpty()) return;

        iiifResource.Behavior = iiifResource.Behavior.Where(b => !PresentationBehaviours.Contains(b)).ToList();
    }

    /// <summary>
    /// Get only the presentation-specific behaviors (public, storage-collection) from the provided behaviors
    /// </summary>
    /// <returns>Filtered list of behaviors, empty if there are none</returns>
    public static List<string> OnlyPresentationBehaviours(this IEnumerable<string>? behaviours)
        => behaviours?.Where(b => PresentationBehaviours.Contains(b)).ToList() ?? [];
}
