using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using IIIF;
using Models.API.General;
using Models.Database.Collections;

namespace API.Features.Common.Helpers;

public static class ParentValidator
{
    public static ModifyEntityResult<TCollection, ModifyCollectionType>? ValidateParentCollection<TCollection>(Collection? parentCollection) 
        where TCollection : JsonLdBase
    {
        if (parentCollection == null) return ErrorHelper.NullParentResponse<TCollection>();
        
        return !parentCollection.IsStorageCollection ? ErrorHelper.ParentMustBeStorageCollection<TCollection>() : null;
    }
}
