using System.Diagnostics.CodeAnalysis;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using Core.Helpers;
using IIIF;
using Models;
using Models.API;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Repository;
using Repository.Helpers;
using Repository.Paths;

namespace API.Helpers;

/// <summary>
/// Parses API requests to retrieve the parent and slug values
/// </summary>
public interface IParentSlugParser
{
    /// <summary>
    /// Parse the parent and slug values for a write request.
    /// </summary>
    /// <param name="presentation">The deserialized request body</param>
    /// <param name="customerId">Customer id from the request URL</param>
    /// <param name="id">Internal id from the request URL, for flat PUT only</param>
    /// <param name="urlParentPath">
    /// Parent path for the resource - for hierarchical requests, the full path for POST (the container being
    /// POSTed into) or everything but the last segment for PUT; for flat requests, derived from the body's "id"
    /// property when it resolves to an own-host hierarchical id (see <see cref="IRequestIdResolver"/>)
    /// </param>
    /// <param name="urlSlug">
    /// Slug for the resource - for hierarchical PUT, the last segment of the path; for flat requests, derived from
    /// the body's "id" property when it resolves to an own-host hierarchical id
    /// </param>
    public Task<ParsedParentSlugResult> Parse<T>(
        T presentation,
        int customerId,
        string? id,
        string? urlParentPath = null,
        string? urlSlug = null,
        CancellationToken cancellationToken = default)
        where T : JsonLdBase, IPresentation;
}

public class ParentSlugParser(PresentationContext dbContext,
    IHttpContextAccessor contextAccessor,
    IPathRewriteParser pathRewriteParser,
    ILogger<ParentSlugParser> logger) : IParentSlugParser
{
    public async Task<ParsedParentSlugResult> Parse<T>(T presentation,
        int customerId, string? id, string? urlParentPath = null, string? urlSlug = null,
        CancellationToken cancellationToken = default)
        where T : JsonLdBase, IPresentation
    {
        if (IsRoot(presentation, id))
        {
            var rootError = TryValidateRoot(presentation, customerId);
            if (rootError != null) return ParsedParentSlugResult.Fail(rootError);

            logger.LogDebug("'{Id}' is Root collection, returning default ParserResult", id);
            return ParsedParentSlugResult.Success(ParsedParentSlug.RootCollection);
        }

        // Try and match slug, if invalid this is cheaper than parent validation so do first
        var (slugErrors, slug) = TryGetSlug(presentation, urlSlug);
        if (slugErrors != null)
        {
            return ParsedParentSlugResult.Fail(slugErrors);
        }

        // For flat requests, PresentationValidator already guarantees a slug/parent or publicId is present. Hierarchical
        // requests don't run that validator (the URL can legitimately supply what the body doesn't), so a missing
        // slug is only caught here.
        if (string.IsNullOrEmpty(slug))
        {
            return ParsedParentSlugResult.Fail(UpsertErrorHelper.MissingSlug());
        }

        if (SpecConstants.ProhibitedSlugs.Contains(slug))
        {
            return ParsedParentSlugResult.Fail(UpsertErrorHelper.ProhibitedSlug(slug));
        }

        var parent = await TryGetParent(presentation, customerId, urlParentPath, cancellationToken);
        if (parent.Errors != null)
        {
            return ParsedParentSlugResult.Fail(parent.Errors);
        }

        return ParsedParentSlugResult.Success(
            new ParsedParentSlug(parent.Parent.ThrowIfNull(nameof(parent)), slug)
        );
    }

    private static bool IsRoot<T>(T presentation, string? id)
        => presentation is PresentationCollection && id != null && KnownCollections.IsRoot(id);

    private PresentationResult? TryValidateRoot(IPresentation presentation, int customer)
        => string.IsNullOrEmpty(presentation.PublicId) || presentation.PublicIdIsRoot(GetBaseUrl(), customer)
            ? null
            : UpsertErrorHelper.IncorrectPublicId();

    private (PresentationResult? errors, string? slug) TryGetSlug(IPresentation presentation, string? urlSlug)
    {
        // Try and get slug from publicId and/or 'slug' property directly
        var publicIdSlug = presentation.PublicId?.GetLastPathElement();
        var slug = presentation.Slug;

        string? resolvedSlug;
        if (string.IsNullOrEmpty(slug))
        {
            resolvedSlug = publicIdSlug;
        }
        else if (publicIdSlug != null && publicIdSlug != slug)
        {
            logger.LogDebug("PublicId slug '{PublicIdSlug}' and explicit slug {Slug} do not match",
                presentation.PublicId, presentation.Slug);
            return (UpsertErrorHelper.SlugMustMatchPublicId(), null);
        }
        else
        {
            resolvedSlug = slug;
        }

        // Reconcile against slug derived from the request URL (hierarchical PUT only)
        if (urlSlug != null)
        {
            if (resolvedSlug != null && resolvedSlug != urlSlug)
            {
                logger.LogDebug("URL-derived slug '{UrlSlug}' and body-derived slug '{ResolvedSlug}' do not match",
                    urlSlug, resolvedSlug);
                return (UpsertErrorHelper.SlugSourcesDoNotMatch(), null);
            }

            resolvedSlug = urlSlug;
        }

        return (null, resolvedSlug);
    }

    private async Task<ParsedParent> TryGetParent(IPresentation presentation, int customerId, string? urlParentPath,
        CancellationToken cancellationToken)
    {
        var parent = await TryGetParentFromPresentation(presentation, customerId, cancellationToken);
        if (parent.Errors != null) return parent;

        // Reconcile against parent derived from the request URL (hierarchical POST/PUT only)
        if (urlParentPath != null)
        {
            var urlParentHierarchy = await dbContext.RetrieveHierarchy(customerId, urlParentPath, cancellationToken);
            var urlParentCollection = urlParentHierarchy?.Collection;

            if (parent.Parent != null && urlParentCollection != null && parent.Parent.Id != urlParentCollection.Id)
            {
                logger.LogDebug(
                    "URL-derived parent '{UrlParentPath}' and body-derived parent '{BodyParent}' do not match",
                    urlParentPath, parent.Parent.Id);
                return ParsedParent.Fail(UpsertErrorHelper.ParentSourcesDoNotMatch());
            }

            // The URL always wins here - if it doesn't resolve to anything, that's a real error (the caller asked
            // for a specific container), not something a body-derived parent should be allowed to paper over
            parent = ParsedParent.Success(urlParentCollection);
        }

        // Passed values match, validate parent can be used
        var parentValidationError = ParentValidator.ValidateParentCollection(parent.Parent);
        if (parentValidationError != null) return ParsedParent.Fail(parentValidationError);

        return parent;
    }

    private async Task<ParsedParent> TryGetParentFromPresentation(
        IPresentation presentation,
        int customerId,
        CancellationToken cancellationToken)
    {
        // Try and get a parent from publicId
        var publicIdParent = await RetrieveParent(presentation, customerId, true, cancellationToken);

        // If we don't have parent or there are errors, return what we could parse from publicId
        if (publicIdParent.Errors != null || presentation.Parent == null) return publicIdParent;

        // We have Parent property - find Collection for that
        var parent = await RetrieveParent(presentation, customerId, false, cancellationToken);

        if (parent.Errors != null) return parent;

        // Validate that if we have publicId AND parent they are for the same thing
        if (publicIdParent.Parent != null && parent.Parent != null && publicIdParent.Parent.Id != parent.Parent.Id)
        {
            logger.LogDebug("PublicId parent '{PublicIdParent}' and explicit parent {Parent} do not match",
                presentation.PublicId, presentation.Parent);
            return ParsedParent.Fail(UpsertErrorHelper.ParentMustMatchPublicId());
        }

        return parent;
    }

    private async Task<ParsedParent> RetrieveParent(
        IPresentation presentation,
        int customerId,
        bool fromPublicId,
        CancellationToken cancellationToken)
    {
        Uri? parentUri;
        if (fromPublicId)
        {
            if (presentation.PublicId == null) return ParsedParent.Empty();
            parentUri = PathParser.GetParentUriFromPublicId(presentation.PublicId);
        }
        else
        {
            if (!Uri.TryCreate(presentation.Parent, UriKind.Absolute, out parentUri)) return ParsedParent.Empty();
        }

        try
        {
            var parentPath =
                pathRewriteParser.ParsePathWithRewrites(parentUri.Host, parentUri.AbsolutePath,
                    customerId);

            if (parentPath.Resource == null) return ParsedParent.Empty();

            if (parentPath.Customer != customerId)
            {
                return ParsedParent.Fail(UpsertErrorHelper.CustomerIdDoesNotMatchCaller("publicId"));
            }

            if (!parentPath.Hierarchical)
            {
                return ParsedParent.Success(await dbContext.RetrieveCollectionAsync(customerId, parentPath.Resource,
                    cancellationToken: cancellationToken));
            }

            var parentHierarchy =
                await dbContext.RetrieveHierarchy(customerId, parentPath.Resource, cancellationToken);
            var parent = parentHierarchy?.Collection;
            return ParsedParent.Success(parent);
        }
        catch (FormatException fe)
        {
            logger.LogDebug(fe, "Cannot parse parent from public id");
            return ParsedParent.Empty();
        }
    }

    private class ParsedParent
    {
        public Collection? Parent { get; private init; }

        public PresentationResult? Errors { get; private init; }

        public static ParsedParent Fail(PresentationResult errors) =>
            new() { Errors = errors};

        public static ParsedParent Success(Collection? parent) => new() { Parent = parent };

        public static ParsedParent Empty() => new();
    }

    private string GetBaseUrl() => contextAccessor.HttpContext!.Request.GetBaseUrl();
}

public class ParsedParentSlug
{
    public Collection? Parent { get; private init; }

    public string Slug { get; private init; }

    private ParsedParentSlug()
    {
        Slug = string.Empty;
        Parent = null;
    }

    public ParsedParentSlug(Collection parent, string slug)
    {
        Parent = parent;
        Slug = slug;
    }

    public static readonly ParsedParentSlug RootCollection = new();
}

public class ParsedParentSlugResult
{
    public PresentationResult? Errors { get; private init; }

    public ParsedParentSlug? ParsedParentSlug { get; private init; }

    [MemberNotNullWhen(true, nameof(Errors))]
    [MemberNotNullWhen(false, nameof(ParsedParentSlug))]
    public bool IsError { get; private init; }

    public static ParsedParentSlugResult Fail(PresentationResult errors) =>
        new() { Errors = errors, IsError = true };

    public static ParsedParentSlugResult Success(ParsedParentSlug parsed) => new() { ParsedParentSlug = parsed };
}
