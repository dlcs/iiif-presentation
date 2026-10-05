using Models.Database.General;
using Repository.Paths;

namespace Services.Manifests.Helpers;

public static class PublicIdGenerator
{
    public static string GetPublicId(SettingsBasedPathGenerator settingsBasedPathGenerator, IPathGenerator pathGenerator, Hierarchy hierarchy)
    {
        return settingsBasedPathGenerator.HasPathForCustomer(hierarchy.CustomerId)
            ? settingsBasedPathGenerator.GenerateHierarchicalId(hierarchy)
            : pathGenerator.GenerateHierarchicalId(hierarchy);
    }
    
    public static string GetPublicIdFromFullPath(SettingsBasedPathGenerator settingsBasedPathGenerator, IPathGenerator pathGenerator, int customerId, string fullPath)
    {
        return settingsBasedPathGenerator.HasPathForCustomer(customerId)
            ? settingsBasedPathGenerator.GenerateHierarchicalFromFullPath(customerId, fullPath)
            : pathGenerator.GenerateHierarchicalFromFullPath(customerId, fullPath);
    }
}
