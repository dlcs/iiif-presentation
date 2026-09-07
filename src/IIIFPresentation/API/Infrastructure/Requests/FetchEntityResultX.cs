using API.Features.Storage.Helpers;
using Models.API.Collection;
using Models.API.Manifest;

namespace API.Infrastructure.Requests;

public static class FetchEntityResultX
{
    /// <summary>
    /// The manifest's public hierarchical path, for a result fetched with <c>pathOnly: true</c> (only
    /// <see cref="PresentationManifest.FullPath"/> is populated in that case). Null if not found, or the manifest
    /// has no hierarchical location.
    /// </summary>
    public static string? GetHierarchicalPath(this FetchEntityResult<PresentationManifest> result) =>
        result.Entity?.FullPath is { Length: > 0 } fullPath ? fullPath : null;

    /// <summary>
    /// The collection's public hierarchical path. Null if not found, or the collection isn't public.
    /// </summary>
    public static string? GetHierarchicalPath(this FetchEntityResult<PresentationCollection> result) =>
        result.Entity?.Behavior.IsPublic() == true ? result.Entity.PublicId : null;
}
