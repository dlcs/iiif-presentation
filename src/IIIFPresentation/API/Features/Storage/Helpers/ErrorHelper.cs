using API.Infrastructure.Requests;
using Core;
using Models.API.General;

namespace API.Features.Storage.Helpers;

public static class ErrorHelper
{
    public static ModifyEntityResult<TCollection, ModifyCollectionType> NullParentResponse<TCollection>() 
        where TCollection : class
    {
        return ModifyEntityResult<TCollection, ModifyCollectionType>.Failure(
            "The parent collection could not be found", ModifyCollectionType.ParentCollectionNotFound, WriteResult.Conflict);
    }
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> CannotGenerateUniqueId<TCollection>() 
        where TCollection : class
    {
        return ModifyEntityResult<TCollection, ModifyCollectionType>.Failure(
            "Could not generate a unique identifier.  Please try again",
            ModifyCollectionType.CannotGenerateUniqueId, WriteResult.Error);
    }
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> CannotValidateIIIF<TCollection>() 
        where TCollection : class
    {
        return ModifyEntityResult<TCollection, ModifyCollectionType>.Failure(
            "An error occurred while attempting to validate the collection as IIIF",
            ModifyCollectionType.CannotValidateIIIF, WriteResult.BadRequest);
    }
}