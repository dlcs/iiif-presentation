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
    public Task<ParsedParentSlugResult<T>> Parse<T>(
        T presentation,
        int customerId,
        string? id,
        CancellationToken cancellationToken = default)
        where T : JsonLdBase, IPresentation;
}

public class ParentSlugParser(PresentationContext dbContext,
    IHttpContextAccessor contextAccessor, 
    IPathRewriteParser pathRewriteParser, 
    ILogger<ParentSlugParser> logger) : IParentSlugParser
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

        var parent = await TryGetParent<T>(presentation, customerId, cancellationToken);
        if (parent.Errors != null)
        {
            return ParsedParentSlugResult<T>.Fail(parent.Errors);
        }

        return ParsedParentSlugResult<T>.Success(
            new ParsedParentSlug(parent.Parent.ThrowIfNull(nameof(parent)), slug.ThrowIfNull(nameof(slug)))
        );
    }

    private static bool IsRoot<T>(T presentation, string? id) 
        => presentation is PresentationCollection && id != null && KnownCollections.IsRoot(id);

    private ModifyEntityResult<T, ModifyCollectionType>? TryValidateRoot<T>(IPresentation presentation, int customer)
        where T : JsonLdBase
        => string.IsNullOrEmpty(presentation.PublicId) || presentation.PublicIdIsRoot(GetBaseUrl(), customer)
            ? null
            : UpsertErrorHelper.IncorrectPublicId<T>();

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
            return (UpsertErrorHelper.SlugMustMatchPublicId<T>(), null);
        }

        return (null, slug);
    }

    private async Task<ParsedParent<T>>
        TryGetParent<T>(IPresentation presentation, int customerId, CancellationToken cancellationToken)
        where T : JsonLdBase
    {
        var parent =
            await TryGetParentFromPresentation<T>(presentation, customerId, cancellationToken);
        if (parent.Errors != null) return parent;

        // Passed values match, validate parent can be used
        var parentValidationError = ParentValidator.ValidateParentCollection<T>(parent.Parent);
        if (parentValidationError != null) return ParsedParent<T>.Fail(parentValidationError);

        return parent;
    }

    private async Task<ParsedParent<T>>
        TryGetParentFromPresentation<T>(
            IPresentation presentation,
            int customerId,
            CancellationToken cancellationToken) where T : JsonLdBase
    {
        // Try and get a parent from publicId 
        var publicIdParent = await GetParentFromPublicId<T>(presentation, customerId, cancellationToken);
        
        if (publicIdParent.Errors != null) return publicIdParent;

        // If we don't have parent, return what we could parse from publicId 
        if (presentation.Parent == null) return publicIdParent;

        // We have Parent property - find Collection for that 
        var parent = await RetrieveParentFromPresentation<T>(presentation, customerId, cancellationToken);
        
        if (parent.Errors != null) return parent; 

        // Validate that if we have publicId AND parent they are for the same thing 
        if (publicIdParent.Parent != null && parent.Parent != null && publicIdParent.Parent.Id != parent.Parent.Id)
        {
            logger.LogDebug("PublicId parent '{PublicIdParent}' and explicit parent {Parent} do not match",
                presentation.PublicId, presentation.Parent);
            return ParsedParent<T>.Fail(UpsertErrorHelper.ParentMustMatchPublicId<T>());
        }

        return parent;
    }

    private async Task<ParsedParent<T>> 
        GetParentFromPublicId<T>(
            IPresentation presentation,
            int customerId, 
            CancellationToken cancellationToken)  where T : JsonLdBase
    {
        if (presentation.PublicId == null) return new ParsedParent<T>();

        // Lookup the parent Collection, handling Api and Public paths
        var publicIdParentUri = PathParser.GetParentUriFromPublicId(presentation.PublicId);

        try
        {
            var parentPath =
                pathRewriteParser.ParsePathWithRewrites(publicIdParentUri.Host, publicIdParentUri.AbsolutePath,
                    customerId);
            
            if (parentPath.Resource == null) return new ParsedParent<T>();

            if (parentPath.Customer != customerId)
            {
                return ParsedParent<T>.Fail(UpsertErrorHelper.CustomerIdDoesNotMatchCaller<T>("publicId", parentPath.Customer!.Value, customerId));
            }
            
            var publicIdParentHierarchy =
                await dbContext.RetrieveHierarchy(customerId, parentPath.Resource, cancellationToken);
            var publicIdParent = publicIdParentHierarchy?.Collection;
            return ParsedParent<T>.Success(publicIdParent);
        }
        catch (FormatException fe)
        {
            logger.LogDebug(fe, "Cannot parse parent from public id");
            return new ParsedParent<T>();
        }
    }
    
    private async Task<ParsedParent<T>> 
        RetrieveParentFromPresentation<T>(
            IPresentation presentation, 
            int customerId,
            CancellationToken cancellationToken) where T : JsonLdBase
    {
        if (Uri.TryCreate(presentation.Parent, UriKind.Absolute, out var parentUri) is not true) return new ParsedParent<T>();
        var parentPath = pathRewriteParser.ParsePathWithRewrites(parentUri.Host, parentUri.AbsolutePath, customerId);

        if (parentPath.Resource == null) return new ParsedParent<T>();
        
        if (parentPath.Customer != customerId)
        {
            return ParsedParent<T>.Fail(UpsertErrorHelper.CustomerIdDoesNotMatchCaller<T>("parent", parentPath.Customer!.Value, customerId));
        }

        if (!parentPath.Hierarchical)
        {
            return ParsedParent<T>.Success(await dbContext.RetrieveCollectionAsync(customerId, parentPath.Resource,
                cancellationToken: cancellationToken));
        }
        
        var parentHierarchy = await dbContext.RetrieveHierarchy(customerId, parentPath.Resource,
            cancellationToken);
        var parent = parentHierarchy?.Collection;
        
        if (parent != null) parent.Hierarchy.GetCanonical().FullPath = parentPath.Resource;
        
        return ParsedParent<T>.Success(parent);
    }

    private class ParsedParent<T> where T : JsonLdBase
    {
        public Collection? Parent { get; private init; }
        
        public ModifyEntityResult<T, ModifyCollectionType>? Errors { get; private init; }
        
        public static ParsedParent<T> Fail(ModifyEntityResult<T, ModifyCollectionType> errors) =>
            new() { Errors = errors};
        
        public static ParsedParent<T> Success(Collection? parent) => new() { Parent = parent };
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
