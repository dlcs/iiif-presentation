using Core.Helpers;
using Core.Infrastructure;
using IIIF.Presentation.V3;

namespace Core.IIIF;

public static class BehaviorHelper
{
    public static void RemovePresentationBehaviours(this ResourceBase iiifResource)
    {
        var toRemove = new[] { Behavior.IsStorageCollection, Behavior.IsPublic };
        if (iiifResource.Behavior.IsNullOrEmpty()) return;
        
        iiifResource.Behavior = iiifResource.Behavior.Where(b => !toRemove.Contains(b)).ToList();
    }
}
