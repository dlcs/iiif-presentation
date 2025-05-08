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
    /// Helper method that returns slug, id and assetId values from .
    /// </summary>
    public static (string slug, string id, string assetId) SlugResourceAsset([CallerMemberName] string testMethod = "")
        => (testMethod, $"{testMethod}_id", testMethod);
}
