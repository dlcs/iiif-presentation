using API.Infrastructure.Requests;
using IIIF;
using Models.API;

namespace API.Helpers;

public static class ParentSlugParserX
{
    /// <summary>
    /// Resolves the parent/slug for a write request, unwrapping the result into an error-or-value pair - shared by
    /// <c>CollectionWriteService</c> and <c>ManifestWriteService</c>, whose write requests differ in type but both
    /// carry the same URL-derived parent path/slug hints.
    /// </summary>
    public static async Task<(PresentationResult? error, ParsedParentSlug? parsedParentSlug)> ParseParentSlug<T>(
        this IParentSlugParser parentSlugParser,
        T presentation,
        int customerId,
        string? id,
        string? urlParentPath,
        string? urlSlug,
        CancellationToken cancellationToken)
        where T : JsonLdBase, IPresentation
    {
        var result = await parentSlugParser.Parse(presentation, customerId, id, urlParentPath: urlParentPath,
            urlSlug: urlSlug, cancellationToken: cancellationToken);
        return result.IsError ? (result.Errors, null) : (null, result.ParsedParentSlug);
    }
}
