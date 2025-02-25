using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Validation;
using IIIF;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Repository.Paths;

namespace API.Features.Common.Helpers;

public static class ParentValidator
{
    /// <summary>
    /// Validates that a parent collection is not null or a IIIF collection
    /// </summary>
    public static ModifyEntityResult<TCollection, ModifyCollectionType>? ValidateParentCollection<TCollection>(Collection? parentCollection) 
        where TCollection : JsonLdBase
    {
        if (parentCollection == null) return ErrorHelper.NullParentResponse<TCollection>();
        
        return !parentCollection.IsStorageCollection ? ErrorHelper.ParentMustBeStorageCollection<TCollection>() : null;
    }
    
    /// <summary>
    /// Validates that a parent collection is not null, a IIIF collection or that the URI is invalid
    /// </summary>
    public static ModifyEntityResult<PresentationCollection, ModifyCollectionType>? ValidateParentCollection(Collection? parentCollection, 
        PresentationCollection presentationCollection, IPathGenerator pathGenerator)
    {
        var error = ValidateParentCollection<PresentationCollection>(parentCollection);
        
        if (error != null) return error;
        
        return presentationCollection.IsUriParentInvalid(parentCollection!, pathGenerator)
            ? ErrorHelper.NullParentResponse<PresentationCollection>()
            : null;
    }
}
