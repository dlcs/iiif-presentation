using Core.Infrastructure;

namespace API.Features.Storage.Helpers;

public static class BehaviorX
{
    public static bool IsPublic(this List<string>? behaviors)
    {
        return behaviors?.Contains(Behavior.IsPublic) ?? false;
    }
    
    public static bool IsStorageCollection(this List<string>? behaviors)
    {
        return behaviors?.Contains(Behavior.IsStorageCollection) ?? false;
    }
}
