using System.Runtime.CompilerServices;

namespace API.Tests.Integration.Infrastructure;

/// <summary>
/// Helper methods for generating test identifiers, allows for distinct values per test method via
/// <see cref="CallerMemberNameAttribute"/> but keeps call site cleaner and avoids copy/paster issues inherent with
/// nameof() usage 
/// </summary>
public static class TestIdentifiers
{
    /// <summary>
    /// Helper method that returns single id
    /// </summary>
    public static string Id([CallerMemberName] string testMethod = "") => testMethod;
    
    /// <summary>
    /// Helper method that returns slug and id
    /// </summary>
    public static (string slug, string id) SlugResource([CallerMemberName] string testMethod = "")
        => (testMethod, $"{testMethod}_id");
    
    /// <summary>
    /// Helper method that returns slug, id and assetId values
    /// </summary>
    public static (string slug, string id, string assetId) SlugResourceAsset([CallerMemberName] string testMethod = "")
        => (testMethod, $"{testMethod}_id", testMethod);

    /// <summary>
    /// Helper method that returns slug, id assetId and canvasId values
    /// </summary>
    public static (string slug, string id, string assetId, string canvasId) SlugResourceAssetCanvas(
        [CallerMemberName] string testMethod = "")
        => (testMethod, $"{testMethod}_id", testMethod, $"{testMethod}_c");

    private static int batchId = 1;

    /// <summary>
    /// Generate a random BatchId, ensures there are no collisions
    /// </summary>
    public static int BatchId()
        => Interlocked.Increment(ref batchId);
}
