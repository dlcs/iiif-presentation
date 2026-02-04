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
        if (parentCollection == null) return UpsertErrorHelper.NullParentResponse<TCollection>();
        
        return !parentCollection.IsStorageCollection ? UpsertErrorHelper.ParentMustBeStorageCollection<TCollection>() : null;
    }
}
