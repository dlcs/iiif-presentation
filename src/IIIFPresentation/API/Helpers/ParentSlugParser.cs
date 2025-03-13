using System.Diagnostics.CodeAnalysis;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Validation;
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
/// Parses API requests to retrieve the parent a nd slug values
/// </summary>
public interface IParentSlugParser
{
    public Task<ParsedParentSlugResult<T>> Parse<T>(
        T presentation,
        int customerId,
        string? id,
        CancellationToken cancellationToken = default)
        where T : JsonLdBase, IPresentation;
}

public class ParentSlugParser(PresentationContext dbContext, IPathGenerator pathGenerator, IHttpContextAccessor contextAccessor, ILogger<ParentSlugParser> logger) : IParentSlugParser
{
    public async Task<ParsedParentSlugResult<T>> Parse<T>(T presentation,
        int customerId, string? id, CancellationToken cancellationToken = default)
        where T : JsonLdBase, IPresentation
    {
        if (IsRoot(presentation, id))
        {
            var rootError = TryValidateRoot<T>(presentation, customerId);
            if (rootError != null) return ParsedParentSlugResult<T>.Fail(rootError);

            logger.LogDebug("'{Id}' is Root collection, returning default ParserResult", id);
            return ParsedParentSlugResult<T>.Success(ParsedParentSlug.RootCollection);
        }

        // Try and match slug, if invalid this is cheaper than parent validation so do first
        var (slugErrors, slug) = TryGetSlug<T>(presentation);
        if (slugErrors != null)
        {
            return ParsedParentSlugResult<T>.Fail(slugErrors);
        }

        var (parentErrors, parent) = await TryGetParent<T>(presentation, customerId, cancellationToken);
        if (parentErrors != null)
        {
            return ParsedParentSlugResult<T>.Fail(parentErrors);
        }

        return ParsedParentSlugResult<T>.Success(
            new ParsedParentSlug(parent.ThrowIfNull(nameof(parent)), slug.ThrowIfNull(nameof(slug)))
        );
    }

    private static bool IsRoot<T>(T presentation, string? id) 
        => presentation is PresentationCollection && id != null && KnownCollections.IsRoot(id);

    private ModifyEntityResult<T, ModifyCollectionType>? TryValidateRoot<T>(IPresentation presentation, int customer)
        where T : JsonLdBase
        => string.IsNullOrEmpty(presentation.PublicId) || presentation.PublicIdIsRoot(GetBaseUrl(), customer)
            ? null
            : ErrorHelper.IncorrectPublicId<T>();

    private (ModifyEntityResult<T, ModifyCollectionType>? errors, string? slug)
        TryGetSlug<T>(IPresentation presentation) where T : JsonLdBase
    {
        // Try and get slug from publicId and/or 'slug' property directly
        var publicIdSlug = presentation.PublicId?.GetLastPathElement();
        var slug = presentation.Slug;

        if (string.IsNullOrEmpty(slug)) return (null, publicIdSlug);

        if (publicIdSlug != null && publicIdSlug != slug)
        {
            logger.LogDebug("PublicId slug '{PublicIdSlug}' and explicit slug {Slug} do not match",
                presentation.PublicId, presentation.Slug);
            return (ErrorHelper.SlugMustMatchPublicId<T>(), null);
        }

        return (null, slug);
    }

    private async Task<(ModifyEntityResult<T, ModifyCollectionType>? errors, Collection? parent)>
        TryGetParent<T>(IPresentation presentation, int customerId, CancellationToken cancellationToken)
        where T : JsonLdBase
    {
        var (parentErrors, parent) =
            await TryGetParentFromPresentation<T>(presentation, customerId, cancellationToken);
        if (parentErrors != null) return (parentErrors, parent);

        // Passed values match, validate parent can be used
        var parentValidationError = ParentValidator.ValidateParentCollection<T>(parent);
        if (parentValidationError != null) return (parentValidationError, null);

        if (presentation.IsParentInvalid(parent, contextAccessor.HttpContext!.Request.GetBaseUrl(), customerId,
                pathGenerator))
        {
            return (ErrorHelper.NullParentResponse<T>(), null);
        }

        return (null, parent);
    }

    private async Task<(ModifyEntityResult<T, ModifyCollectionType>? errors, Collection? parent)>
        TryGetParentFromPresentation<T>(
            IPresentation presentation,
            int customerId,
            CancellationToken cancellationToken) where T : JsonLdBase
    {
        // Try and get a parent from publicId 
        var publicIdParent = await GetParentFromPublicId(presentation, customerId, cancellationToken);

        // If we don't have parent, return what we could parse from publicId 
        if (presentation.Parent == null) return (null, publicIdParent);

        // We have Parent property - find Collection for that 
        var parent = await RetrieveParentFromPresentation(presentation, customerId, cancellationToken);

        // Validate that if we have publicId AND parent they are for same thing 
        if (publicIdParent != null && parent != null && publicIdParent.Id != parent.Id)
        {
            logger.LogDebug("PublicId parent '{PublicIdParent}' and explicit parent {Parent} do not match",
                presentation.PublicId, presentation.Parent);
            return (ErrorHelper.ParentMustMatchPublicId<T>(), null);
        }

        return (null, parent);
    }

    private async Task<Collection?> GetParentFromPublicId(IPresentation presentation, int customerId, CancellationToken cancellationToken)
    {
        if (presentation.PublicId == null) return null;

        // Lookup the parent Collection, handling Api and Public paths
        var publicIdParentUri = PathParser.GetParentUriFromPublicId(presentation.PublicId);
        var publicIdParentHierarchy = await dbContext.RetrieveHierarchy(customerId,
            PathParser.GetHierarchicalFullPathFromPath(publicIdParentUri.AbsoluteUri, customerId,
                contextAccessor.HttpContext!.Request.GetBaseUrl()), cancellationToken);
        var publicIdParent = publicIdParentHierarchy?.Collection;
        return publicIdParent;
    }

    private async Task<Collection?> RetrieveParentFromPresentation(IPresentation presentation, int customerId,
        CancellationToken cancellationToken)
    {
        var baseUrl = GetBaseUrl();
        if (presentation.ParentIsFlatForm(baseUrl, customerId))
        {
            return await dbContext.RetrieveCollectionAsync(customerId,
                presentation.GetParentSlug(), cancellationToken: cancellationToken);
        }

        var parentFullPath = PathParser.GetHierarchicalFullPathFromPath(presentation.Parent!, customerId, baseUrl);

        var parentHierarchy = await dbContext.RetrieveHierarchy(customerId, parentFullPath,
            cancellationToken: cancellationToken);
        var parent = parentHierarchy?.Collection;

        if (parent != null)
        {
            parent.Hierarchy.GetCanonical().FullPath = parentFullPath;
        }

        return parent;
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

public class ParsedParentSlugResult<T>
    where T : JsonLdBase
{
    public ModifyEntityResult<T, ModifyCollectionType>? Errors { get; private init; }

    public ParsedParentSlug? ParsedParentSlug { get; private init; }

    [MemberNotNullWhen(true, nameof(Errors))]
    [MemberNotNullWhen(false, nameof(ParsedParentSlug))]
    public bool IsError { get; private init; }

    public static ParsedParentSlugResult<T> Fail(ModifyEntityResult<T, ModifyCollectionType> errors) =>
        new() { Errors = errors, IsError = true };

    public static ParsedParentSlugResult<T> Success(ParsedParentSlug parsed) => new() { ParsedParentSlug = parsed };
}
