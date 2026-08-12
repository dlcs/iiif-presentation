using API.Infrastructure.Requests;
using IIIF;
using Models.API;

namespace API.Helpers;

public static class ParentSlugParserX
{
    /// <summary>
    /// Resolves the parent/slug for a write request, unwrapping the result into an error-or-value pair.
    /// </summary>
    public static async Task<(PresentationResult? error, ParsedParentSlug? parsedParentSlug)> ParseParentSlug<T>(
        this IParentSlugParser parentSlugParser,
        T presentation,
        int customerId,
        string? id,
        ResolvedLocation location,
        CancellationToken cancellationToken)
        where T : JsonLdBase, IPresentation
    {
        var result = await parentSlugParser.Parse(presentation, customerId, id, urlParentPath: location.ParentPath,
            urlSlug: location.Slug, cancellationToken: cancellationToken);
        return result.IsError ? (result.Errors, null) : (null, result.ParsedParentSlug);
    }
}
