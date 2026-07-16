using API.Infrastructure.Requests;
using Core;
using Models.API.General;
using Services.Manifests.Exceptions;

namespace API.Features.Common.Helpers;

public static class UpsertErrorHelper
{
    public static ModifyEntityResult<ModifyCollectionType> NullParentResponse()
    {
        return ModifyEntityResult<ModifyCollectionType>.Failure(
            "The parent collection could not be found", ModifyCollectionType.ParentCollectionNotFound, WriteResult.BadRequest);
    }

    public static ModifyEntityResult<ModifyCollectionType> CannotGenerateUniqueId()
    {
        return ModifyEntityResult<ModifyCollectionType>.Failure(
            "Could not generate a unique identifier.  Please try again",
            ModifyCollectionType.CannotGenerateUniqueId, WriteResult.Error);
    }

    public static ModifyEntityResult<ModifyCollectionType> CannotValidateIIIF()
    {
        return ModifyEntityResult<ModifyCollectionType>.Failure(
            "An error occurred while attempting to validate the collection as IIIF",
            ModifyCollectionType.CannotValidateIIIF, WriteResult.BadRequest);
    }

    public static ModifyEntityResult<ModifyCollectionType> CannotChangeCollectionType(bool storageCollection)
    {
        return ModifyEntityResult<ModifyCollectionType>.Failure(
            $"Cannot convert a {CollectionType(storageCollection)} collection to a {CollectionType(!storageCollection)} collection",
            ModifyCollectionType.CannotChangeCollectionType, WriteResult.BadRequest);
    }

    public static ModifyEntityResult<ModifyCollectionType> EtagNotRequired()
    {
        return ModifyEntityResult<ModifyCollectionType>.Failure(
            "ETag should not be included in request when inserting via PUT", ModifyCollectionType.ETagNotAllowed,
            WriteResult.PreconditionFailed);
    }

    public static ModifyEntityResult<ModifyCollectionType> EtagNonMatching()
    {
        return ModifyEntityResult<ModifyCollectionType>.Failure(
            "ETag does not match", ModifyCollectionType.ETagNotMatched, WriteResult.PreconditionFailed);
    }

    public static ModifyEntityResult<ModifyCollectionType> DlcsError(string message)
        => ModifyEntityResult<ModifyCollectionType>.Failure(
            message, ModifyCollectionType.DlcsError, WriteResult.Error);

    public static ModifyEntityResult<ModifyCollectionType> SpaceRequired()
        => ModifyEntityResult<ModifyCollectionType>.Failure(
            "A request with assets requires the space header to be set", ModifyCollectionType.RequiresSpaceHeader,
            WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> CouldNotRetrieveAssetId()
        => ModifyEntityResult<ModifyCollectionType>.Failure(
            "Could not retrieve an id from an attached asset", ModifyCollectionType.CouldNotRetrieveAssetId,
            WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> ParentMustBeStorageCollection()
        => ModifyEntityResult<ModifyCollectionType>.Failure("The parent must be a storage collection",
            ModifyCollectionType.ParentMustBeStorageCollection, WriteResult.Conflict);

    public static ModifyEntityResult<ModifyCollectionType> ParentMustMatchPublicId()
        => ModifyEntityResult<ModifyCollectionType>.Failure("The parent must match the one specified in the public id",
            ModifyCollectionType.ParentMustMatchPublicId, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> SlugMustMatchPublicId()
        => ModifyEntityResult<ModifyCollectionType>.Failure("The slug must match the one specified in the public id",
            ModifyCollectionType.SlugMustMatchPublicId, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> InvalidCanvasId(string? canvasId, string reason)
        => ModifyEntityResult<ModifyCollectionType>.Failure($"The canvas id {canvasId} is invalid - {reason}",
            ModifyCollectionType.InvalidCanvasId, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> InvalidCanvasId(params (string canvasId, string reason)[] multiple)
    {
        var failures = string.Join(", ", multiple.Select(p => $"{p.canvasId}: {p.reason}"));
        var message = $"Errors encountered when parsing painted resources: {failures}.";

        return ModifyEntityResult<ModifyCollectionType>.Failure(message,
            ModifyCollectionType.InvalidCanvasId, WriteResult.BadRequest);
    }

    public static ModifyEntityResult<ModifyCollectionType> ErrorMergingPaintedResourcesWithItems(string error)
        => ModifyEntityResult<ModifyCollectionType>.Failure(error,
            ModifyCollectionType.ErrorMergingPaintedResourcesWithItems, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> DuplicateCanvasId(string? canvasId)
        => ModifyEntityResult<ModifyCollectionType>.Failure($"The canvas ID {canvasId} cannot be a duplicate",
            ModifyCollectionType.DuplicateCanvasId, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> IncorrectPublicId()
        => ModifyEntityResult<ModifyCollectionType>.Failure("publicId incorrect",
            ModifyCollectionType.PublicIdIncorrect, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> PaintableAssetError(string error)
        => ModifyEntityResult<ModifyCollectionType>.Failure(error,
            ModifyCollectionType.PaintableAssetError, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> AssetError(AssetException exception)
        => ModifyEntityResult<ModifyCollectionType>.Failure(exception.Message,
            ModifyCollectionType.AssetError, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> CustomerIdDoesNotMatchCaller(string field)
        => ModifyEntityResult<ModifyCollectionType>.Failure($"The {field} has a customer id that does not match the customer id found on the calling URL",
            ModifyCollectionType.CustomerIdDoesNotMatchCaller, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> AssetAdjunctsDoNotMatch(string assetId, string diff)
        => ModifyEntityResult<ModifyCollectionType>.Failure(
            $"Asset {assetId} is specified multiple times with different adjuncts - diff: {diff}",
            ModifyCollectionType.AssetsAdjunctsDoNotMatch, WriteResult.BadRequest);

    public static ModifyEntityResult<ModifyCollectionType> ManifestCurrentlyIngesting()
        => ModifyEntityResult<ModifyCollectionType>.Failure(
            "The manifest is currently being ingested and cannot be modified",
            ModifyCollectionType.ManifestCurrentlyIngesting, WriteResult.Conflict);

    public static ModifyEntityResult<ModifyCollectionType> AssetsDataDoesNotMatch(string assetId, string diff)
        => ModifyEntityResult<ModifyCollectionType>.Failure(
            $"Asset {assetId} is specified multiple times, but has conflicting data - diff: {diff}",
            ModifyCollectionType.AssetsDoNotMatch, WriteResult.BadRequest);

    private static string CollectionType(bool isStorageCollection)
    {
        return isStorageCollection ? "Storage" : "IIIF";
    }
}
