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
    public Task<(ModifyEntityResult<TPresentation, ModifyCollectionType>? errors, ParsedParentSlug? parsedParentSlug)> Parse
        <TPresentation>(IPresentation presentation, int customerId, CancellationToken cancellationToken = default) where TPresentation : JsonLdBase;
}

public class ParentSlugParser(PresentationContext dbContext, IPathGenerator pathGenerator, IHttpContextAccessor contextAccessor) : IParentSlugParser
{
    
    public async Task<(ModifyEntityResult<TPresentation, ModifyCollectionType>? errors, ParsedParentSlug? parsedParentSlug)>
        Parse<TPresentation>(IPresentation presentation, int customerId, CancellationToken cancellationToken = default) where TPresentation : JsonLdBase
    {
        Collection? parent;
        string? slug;
        Collection? publicIdParent = null;
        string? publicIdSlug = null;

        if (presentation.PublicId != null)
        {
            publicIdSlug = presentation.PublicId.GetLastPathElement();
            var publicIdParentUri = new Uri(presentation.PublicId.Substring(0, presentation.PublicId.LastIndexOf('/')));
            var publicIdParentHierarchy = await dbContext.RetrieveHierarchy(customerId,
                PathParser.GetHierarchicalSlugFromPath(presentation.Parent, customerId,
                    contextAccessor.HttpContext!.Request.GetBaseUrl()), cancellationToken);
            publicIdParent = publicIdParentHierarchy?.Collection;
        }
        
        if (presentation.Parent != null)
        {
            parent = await RetrieveParentFromPresentation(presentation, customerId, cancellationToken);
            
            if (publicIdParent != null && parent != null && publicIdParent.Id != parent.Id)
            {
                return (ErrorHelper.ParentMustMatchPublicId<TPresentation>(), null);
            }
            
            if (parent == null && publicIdParent != null)
            {
                parent = publicIdParent;
            }
        }
        else
        {
            parent = publicIdParent;
        }

        if (presentation.Slug != null)
        {
            slug = presentation.Slug;
            
            if (publicIdSlug != null && slug != null && publicIdSlug != slug)
            {
                return (ErrorHelper.SlugMustMatchPublicId<TPresentation>(), null);
            }
            
            if (slug == null && publicIdSlug != null)
            {
                slug = publicIdSlug;
            }
        }
        else
        {
            slug = publicIdSlug;
        }
        
        // Validation
        var parentValidationError =
            ParentValidator.ValidateParentCollection<TPresentation>(parent);
        if (parentValidationError != null) return (parentValidationError, null);
        if (presentation.IsParentInvalid(parent, pathGenerator))
            return (ErrorHelper.NullParentResponse<TPresentation>(), null);
        
        return (null, new ParsedParentSlug
        {
            Parent = parent,
            Slug = slug
        });
    }

    private async Task<Collection?> RetrieveParentFromPresentation(IPresentation presentation, int customerId, CancellationToken cancellationToken = default)
    {
        if (presentation.ParentIsFlatForm())
        {
            return await dbContext.RetrieveCollectionAsync(customerId,
                presentation.GetParentSlug(), cancellationToken: cancellationToken);
        }

        var parentSlug = PathParser.GetHierarchicalSlugFromPath(presentation.Parent, customerId, contextAccessor.HttpContext!.Request.GetBaseUrl());
            
        var parentHierarchy = await dbContext.RetrieveHierarchy(customerId, parentSlug,
            cancellationToken: cancellationToken);
        var parent = parentHierarchy?.Collection;

        if (parent != null)
        {
            parent.Hierarchy.GetCanonical().FullPath = parentSlug;
        }

        return parent;
    }
}

public class ParsedParentSlug
{
    public Collection? Parent { get; set; }
    
    public string? Slug { get; set; }
}

