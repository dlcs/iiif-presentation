using System.Diagnostics.CodeAnalysis;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Validation;
using Core.Helpers;
using IIIF;
using Models.API;
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
    public Task<ParsedParentSlugResult<TPresentation>> Parse<TPresentation>(IPresentation presentation, int customerId, CancellationToken cancellationToken = default) 
        where TPresentation : JsonLdBase;
}

public class ParentSlugParser(PresentationContext dbContext, IPathGenerator pathGenerator, IHttpContextAccessor contextAccessor) : IParentSlugParser
{
    
    public async Task<ParsedParentSlugResult<TPresentation>>
        Parse<TPresentation>(IPresentation presentation, int customerId, CancellationToken cancellationToken = default) where TPresentation : JsonLdBase
    {
        // tracks values set from the public id
        Collection? publicIdParent = null;
        string? publicIdSlug = null;
        
        // tracks values set directly by the properties
        Collection? parent;
        string? slug;

        if (presentation.PublicId != null)
        {
            (publicIdSlug, publicIdParent) =
                await SetValuesFromPublicId(presentation, customerId, cancellationToken);
        }
        
        if (presentation.Parent != null)
        {
            (var errors, parent) =
                await SetValuesFromParent<TPresentation>(presentation, customerId, cancellationToken, publicIdParent);
            if (errors != null)
            {
                return ParsedParentSlugResult<TPresentation>.Fail(errors);
            }
        }
        else
        {
            parent = publicIdParent;
        }

        if (presentation.Slug != null)
        {
            (var errors, slug) = SetValueFromSlug<TPresentation>(presentation, publicIdSlug);
            if (errors != null)
            {
                return ParsedParentSlugResult<TPresentation>.Fail(errors);
            }
        }
        else
        {
            slug = publicIdSlug;
        }
        
        // Validation
        var parentValidationError =
            ParentValidator.ValidateParentCollection<TPresentation>(parent);
        if (parentValidationError != null) return ParsedParentSlugResult<TPresentation>.Fail(parentValidationError);
        if (presentation.IsParentInvalid(parent, contextAccessor.HttpContext!.Request.GetBaseUrl(), customerId, pathGenerator))
            return ParsedParentSlugResult<TPresentation>.Fail(ErrorHelper.NullParentResponse<TPresentation>());

        return ParsedParentSlugResult<TPresentation>.Success(new ParsedParentSlug
        {
            Parent = parent,
            Slug = slug
        });
    }

    private static (ModifyEntityResult<TPresentation, ModifyCollectionType>? errors, string? slug) SetValueFromSlug<TPresentation>(IPresentation presentation, string? publicIdSlug) 
        where TPresentation : JsonLdBase
    {
        var slug = presentation.Slug;

        if (publicIdSlug != null && slug != null && publicIdSlug != slug)
        {
            return (ErrorHelper.SlugMustMatchPublicId<TPresentation>(), null);
        }

        return (null, slug);
    }

    private async Task<(ModifyEntityResult<TPresentation, ModifyCollectionType>? errors, Collection? parent)> 
        SetValuesFromParent<TPresentation>(IPresentation presentation, int customerId,
        CancellationToken cancellationToken, Collection? publicIdParent) where TPresentation : JsonLdBase
    {
        Collection? parent;
        parent = await RetrieveParentFromPresentation(presentation, customerId, cancellationToken);
            
        if (publicIdParent != null && parent != null && publicIdParent.Id != parent.Id)
        {
            return (ErrorHelper.ParentMustMatchPublicId<TPresentation>(), null);
        }
        
        return (null, parent);
    }

    private async Task<(string publicIdSlug, Collection? publicIdParent)> SetValuesFromPublicId(IPresentation presentation, int customerId,
        CancellationToken cancellationToken)
    {
        var publicIdSlug = presentation.PublicId.GetLastPathElement();
        var publicIdParentUri = PathParser.GetParentUriFromPublicId(presentation.PublicId);
        var publicIdParentHierarchy = await dbContext.RetrieveHierarchy(customerId,
            PathParser.GetHierarchicalFullPathFromPath(publicIdParentUri.AbsoluteUri, customerId,
                contextAccessor.HttpContext!.Request.GetBaseUrl()), cancellationToken);
        var publicIdParent = publicIdParentHierarchy?.Collection;
        return (publicIdSlug, publicIdParent);
    }

    private async Task<Collection?> RetrieveParentFromPresentation(IPresentation presentation, int customerId, CancellationToken cancellationToken = default)
    {
        if (presentation.ParentIsFlatForm(contextAccessor.HttpContext!.Request.GetBaseUrl(), customerId))
        {
            return await dbContext.RetrieveCollectionAsync(customerId,
                presentation.GetParentSlug(), cancellationToken: cancellationToken);
        }

        var parentFullPath = PathParser.GetHierarchicalFullPathFromPath(presentation.Parent, customerId, contextAccessor.HttpContext!.Request.GetBaseUrl());
            
        var parentHierarchy = await dbContext.RetrieveHierarchy(customerId, parentFullPath,
            cancellationToken: cancellationToken);
        var parent = parentHierarchy?.Collection;

        if (parent != null)
        {
            parent.Hierarchy.GetCanonical().FullPath = parentFullPath;
        }

        return parent;
    }
}

public class ParsedParentSlug
{
    public required Collection Parent { get; set; }
    
    public required string Slug { get; set; }
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

