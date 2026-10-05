using API.Features.Common.Helpers;
using API.Infrastructure.Requests;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Services.Manifests.Settings;

namespace API.Helpers;

/// <summary>
/// Resolves the base IIIF <c>"id"</c> property supplied in a write request body into an internal flat id, or a
/// hierarchical parent path + slug. An id that isn't a well-formed absolute URI, or doesn't resolve to a
/// recognised host for the customer, is treated as external/opaque and ignored.
/// </summary>
public interface IRequestIdResolver
{
    public ResolvedRequestId Resolve(int customerId, string? bodyId);
}

public class RequestIdResolver(IOptions<PathSettings> options, IPathRewriteParser pathRewriteParser)
    : IRequestIdResolver
{
    private readonly PathSettings settings = options.Value;

    public ResolvedRequestId Resolve(int customerId, string? bodyId)
    {
        if (string.IsNullOrEmpty(bodyId)) return ResolvedRequestId.None;

        if (!Uri.TryCreate(bodyId, UriKind.Absolute, out var idUri))
        {
            // Bare, non-URI id - no existing resource set to validate it against at create time, so it's noise
            return ResolvedRequestId.None;
        }

        if (!settings.IsCustomerRecognisedHost(customerId, idUri.Host))
        {
            // Doesn't belong to us - e.g. copy/pasted from an external source - ignore
            return ResolvedRequestId.None;
        }

        var parsed = pathRewriteParser.ParsePathWithRewrites(idUri.Host, idUri.AbsolutePath, customerId);
        if (parsed.Resource == null) return ResolvedRequestId.None;

        if (parsed.Customer != customerId)
        {
            return ResolvedRequestId.Failure(UpsertErrorHelper.CustomerIdDoesNotMatchCaller("id"));
        }

        if (!parsed.Hierarchical)
        {
            return ResolvedRequestId.ForFlatId(parsed.Resource);
        }

        var lastSeparator = parsed.Resource.LastIndexOf('/');
        var parentPath = lastSeparator >= 0 ? parsed.Resource[..lastSeparator] : string.Empty;
        var slug = lastSeparator >= 0 ? parsed.Resource[(lastSeparator + 1)..] : parsed.Resource;

        return ResolvedRequestId.ForHierarchical(parentPath, slug);
    }
}

/// <summary>
/// Result of resolving a body-supplied <c>"id"</c> property
/// </summary>
public class ResolvedRequestId
{
    /// <summary>
    /// A trusted, internal flat id - present only when the body <c>"id"</c> resolved to a flat-form resource URI
    /// </summary>
    public string? FlatId { get; private init; }

    /// <summary>
    /// Hierarchical parent path derived from the body <c>"id"</c>, when expressed in hierarchical form
    /// </summary>
    public string? HierarchicalParentPath { get; private init; }

    /// <summary>
    /// Slug derived from the body <c>"id"</c>, when expressed in hierarchical form
    /// </summary>
    public string? Slug { get; private init; }

    public PresentationResult? Error { get; private init; }

    public bool IsError => Error != null;

    /// <summary>
    /// Converts this into the <see cref="ResolvedLocation"/> shape write services accept
    /// </summary>
    public ResolvedLocation ToLocation() => new(HierarchicalParentPath, Slug, FlatId);

    public static readonly ResolvedRequestId None = new();

    public static ResolvedRequestId ForFlatId(string flatId) => new() { FlatId = flatId };

    public static ResolvedRequestId ForHierarchical(string parentPath, string slug) =>
        new() { HierarchicalParentPath = parentPath, Slug = slug };

    public static ResolvedRequestId Failure(PresentationResult error) => new() { Error = error };
}
