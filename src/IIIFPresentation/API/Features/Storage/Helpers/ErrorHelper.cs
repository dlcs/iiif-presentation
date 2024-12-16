﻿using API.Infrastructure.Requests;
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

    public static ModifyEntityResult<T, ModifyCollectionType> EtagNotRequired<T>()
        where T : class
    {
        return ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "ETag should not be included in request when inserting via PUT", ModifyCollectionType.ETagNotAllowed,
            WriteResult.PreConditionFailed);
    }
    
    public static ModifyEntityResult<T, ModifyCollectionType> EtagNonMatching<T>()
        where T : class
    {
        return ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "ETag does not match", ModifyCollectionType.ETagNotMatched, WriteResult.PreConditionFailed);
    }
    
    public static ModifyEntityResult<T, ModifyCollectionType> ErrorCreatingSpace<T>()
        where T : class
        => ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "Error creating DLCS space", ModifyCollectionType.ErrorCreatingSpace, WriteResult.Error);

    public static ModifyEntityResult<T, ModifyCollectionType> SpaceRequired<T>()
        where T : class
        => ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "A request with assets requires the space header to be set", ModifyCollectionType.RequiresSpace,
            WriteResult.BadRequest);
    
    public static ModifyEntityResult<T, ModifyCollectionType> CouldNotRetrieveAssetId<T>()
        where T : class
        => ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "Could not retrieve an id from an attached asset", ModifyCollectionType.CouldNotRetrieveAssetId,
            WriteResult.BadRequest);

    private static string CollectionType(bool isStorageCollection)
    {
        return isStorageCollection ? "Storage" : "IIIF";
    }
}
