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
            "The parent collection could not be found", ModifyCollectionType.ParentCollectionNotFound, WriteResult.BadRequest);
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
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> CannotChangeCollectionType<TCollection>
        (bool storageCollection) where TCollection : class
    {
        return ModifyEntityResult<TCollection, ModifyCollectionType>.Failure(
            $"Cannot convert a {CollectionType(storageCollection)} collection to a {CollectionType(!storageCollection)} collection",
            ModifyCollectionType.CannotChangeCollectionType, WriteResult.BadRequest);
    }

    private static string CollectionType(bool isStorageCollection)
    {
        return isStorageCollection ? "Storage" : "IIIF";
    }
}