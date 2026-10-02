namespace API.Helpers;

/// <summary>
/// Parent path/slug/id resolved for a write request, from whichever source produced them - the URL for a
/// hierarchical request, or the request body's "id" property for a flat request (see <see cref="IRequestIdResolver"/>).
/// </summary>
/// <param name="ParentPath">
/// Parent path for the resource - the full path for hierarchical POST, everything but the last segment for
/// hierarchical PUT, or derived from the request body's "id" property for a flat request
/// </param>
/// <param name="Slug">
/// Slug for the resource - the last segment of the path for hierarchical PUT, or derived from the request body's
/// "id" property for a flat request
/// </param>
/// <param name="ClientProvidedId">
/// A trusted, internal flat id resolved from the request body's "id" property (create only). When set, this is
/// used as the new resource's id instead of minting a new one - the caller is responsible for having already
/// recognised this as belonging to us; the write service still checks it isn't already in use
/// </param>
public record ResolvedLocation(string? ParentPath = null, string? Slug = null, string? ClientProvidedId = null)
{
    public static readonly ResolvedLocation None = new();
}
