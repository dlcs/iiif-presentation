using API.Infrastructure.Requests;
using Core;
using IIIF;
using Models.API.General;

namespace API.Features.Storage.Helpers;

public static class ErrorHelper
{
    public static ModifyEntityResult<TCollection, ModifyCollectionType> NullParentResponse<TCollection>() 
        where TCollection : JsonLdBase
    {
        return ModifyEntityResult<TCollection, ModifyCollectionType>.Failure(
            "The parent collection could not be found", ModifyCollectionType.ParentCollectionNotFound, WriteResult.BadRequest);
    }
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> CannotGenerateUniqueId<TCollection>() 
        where TCollection : JsonLdBase
    {
        return ModifyEntityResult<TCollection, ModifyCollectionType>.Failure(
            "Could not generate a unique identifier.  Please try again",
            ModifyCollectionType.CannotGenerateUniqueId, WriteResult.Error);
    }
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> CannotValidateIIIF<TCollection>() 
        where TCollection : JsonLdBase
    {
        return ModifyEntityResult<TCollection, ModifyCollectionType>.Failure(
            "An error occurred while attempting to validate the collection as IIIF",
            ModifyCollectionType.CannotValidateIIIF, WriteResult.BadRequest);
    }
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> CannotChangeCollectionType<TCollection>
        (bool storageCollection) where TCollection : JsonLdBase
    {
        return ModifyEntityResult<TCollection, ModifyCollectionType>.Failure(
            $"Cannot convert a {CollectionType(storageCollection)} collection to a {CollectionType(!storageCollection)} collection",
            ModifyCollectionType.CannotChangeCollectionType, WriteResult.BadRequest);
    }

    public static ModifyEntityResult<T, ModifyCollectionType> EtagNotRequired<T>()
        where T : JsonLdBase
    {
        return ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "ETag should not be included in request when inserting via PUT", ModifyCollectionType.ETagNotAllowed,
            WriteResult.PreConditionFailed);
    }
    
    public static ModifyEntityResult<T, ModifyCollectionType> EtagNonMatching<T>()
        where T : JsonLdBase
    {
        return ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "ETag does not match", ModifyCollectionType.ETagNotMatched, WriteResult.PreConditionFailed);
    }
    
    public static ModifyEntityResult<T, ModifyCollectionType> ErrorCreatingSpace<T>()
        where T : JsonLdBase
        => ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "Error creating DLCS space", ModifyCollectionType.ErrorCreatingSpace, WriteResult.Error);

    public static ModifyEntityResult<T, ModifyCollectionType> SpaceRequired<T>()
        where T : JsonLdBase
        => ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "A request with assets requires the space header to be set", ModifyCollectionType.RequiresSpaceHeader,
            WriteResult.BadRequest);
    
    public static ModifyEntityResult<T, ModifyCollectionType> CouldNotRetrieveAssetId<T>()
        where T : JsonLdBase
        => ModifyEntityResult<T, ModifyCollectionType>.Failure(
            "Could not retrieve an id from an attached asset", ModifyCollectionType.CouldNotRetrieveAssetId,
            WriteResult.BadRequest);
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> ParentMustBeStorageCollection<TCollection>()
        where TCollection : JsonLdBase
        => ModifyEntityResult<TCollection, ModifyCollectionType>.Failure("The parent must be a storage collection",
            ModifyCollectionType.ParentMustBeStorageCollection, WriteResult.Conflict);
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> ParentMustMatchPublicId<TCollection>()
        where TCollection : JsonLdBase
        => ModifyEntityResult<TCollection, ModifyCollectionType>.Failure("The parent must match the one specified in the public id",
            ModifyCollectionType.ParentMustMatchPublicId, WriteResult.BadRequest);
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType> SlugMustMatchPublicId<TCollection>()
        where TCollection : JsonLdBase
        => ModifyEntityResult<TCollection, ModifyCollectionType>.Failure("The slug must match the one specified in the public id",
            ModifyCollectionType.SlugMustMatchPublicId, WriteResult.BadRequest);
    
    public static ModifyEntityResult<TCollection, ModifyCollectionType>? InvalidCanvasId<TCollection>(string? canvasId) 
        where TCollection : JsonLdBase
        => ModifyEntityResult<TCollection, ModifyCollectionType>.Failure($"The canvas ID {canvasId} is invalid",
            ModifyCollectionType.InvalidCanvasId, WriteResult.BadRequest);

    public static ModifyEntityResult<TCollection, ModifyCollectionType>? DuplicateCanvasId<TCollection>(string? canvasId)
        where TCollection : JsonLdBase
        => ModifyEntityResult<TCollection, ModifyCollectionType>.Failure($"The canvas ID {canvasId} cannot be a duplicate",
            ModifyCollectionType.DuplicateCanvasId, WriteResult.BadRequest);

    public static ModifyEntityResult<TCollection, ModifyCollectionType>? CanvasOrderDifferentCanvasId<TCollection>(string? canvasId)
        where TCollection : JsonLdBase
        => ModifyEntityResult<TCollection, ModifyCollectionType>.Failure($"The canvas ID {canvasId} must be the same within a choice construct",
            ModifyCollectionType.CanvasOrderHasDifferentCanvasId, WriteResult.BadRequest);

    private static string CollectionType(bool isStorageCollection)
    {
        return isStorageCollection ? "Storage" : "IIIF";
    }
}
