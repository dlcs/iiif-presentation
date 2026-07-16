using API.Infrastructure.Requests;
using Core;
using Models.API.General;
using Services.Manifests.Exceptions;

namespace API.Features.Common.Helpers;

public static class UpsertErrorHelper
{
    public static PresentationResult NullParentResponse()
    {
        return PresentationResult.Failure(
            "The parent collection could not be found", ModifyCollectionType.ParentCollectionNotFound, WriteResult.BadRequest);
    }

    public static PresentationResult CannotGenerateUniqueId()
    {
        return PresentationResult.Failure(
            "Could not generate a unique identifier.  Please try again",
            ModifyCollectionType.CannotGenerateUniqueId, WriteResult.Error);
    }

    public static PresentationResult CannotValidateIIIF()
    {
        return PresentationResult.Failure(
            "An error occurred while attempting to validate the collection as IIIF",
            ModifyCollectionType.CannotValidateIIIF, WriteResult.BadRequest);
    }

    public static PresentationResult CannotChangeCollectionType(bool storageCollection)
    {
        return PresentationResult.Failure(
            $"Cannot convert a {CollectionType(storageCollection)} collection to a {CollectionType(!storageCollection)} collection",
            ModifyCollectionType.CannotChangeCollectionType, WriteResult.BadRequest);
    }

    public static PresentationResult EtagNotRequired()
    {
        return PresentationResult.Failure(
            "ETag should not be included in request when inserting via PUT", ModifyCollectionType.ETagNotAllowed,
            WriteResult.PreconditionFailed);
    }

    public static PresentationResult EtagNonMatching()
    {
        return PresentationResult.Failure(
            "ETag does not match", ModifyCollectionType.ETagNotMatched, WriteResult.PreconditionFailed);
    }

    public static PresentationResult DlcsError(string message)
        => PresentationResult.Failure(
            message, ModifyCollectionType.DlcsError, WriteResult.Error);

    public static PresentationResult SpaceRequired()
        => PresentationResult.Failure(
            "A request with assets requires the space header to be set", ModifyCollectionType.RequiresSpaceHeader,
            WriteResult.BadRequest);

    public static PresentationResult CouldNotRetrieveAssetId()
        => PresentationResult.Failure(
            "Could not retrieve an id from an attached asset", ModifyCollectionType.CouldNotRetrieveAssetId,
            WriteResult.BadRequest);

    public static PresentationResult ParentMustBeStorageCollection()
        => PresentationResult.Failure("The parent must be a storage collection",
            ModifyCollectionType.ParentMustBeStorageCollection, WriteResult.Conflict);

    public static PresentationResult ParentMustMatchPublicId()
        => PresentationResult.Failure("The parent must match the one specified in the public id",
            ModifyCollectionType.ParentMustMatchPublicId, WriteResult.BadRequest);

    public static PresentationResult SlugMustMatchPublicId()
        => PresentationResult.Failure("The slug must match the one specified in the public id",
            ModifyCollectionType.SlugMustMatchPublicId, WriteResult.BadRequest);

    public static PresentationResult InvalidCanvasId(string? canvasId, string reason)
        => PresentationResult.Failure($"The canvas id {canvasId} is invalid - {reason}",
            ModifyCollectionType.InvalidCanvasId, WriteResult.BadRequest);

    public static PresentationResult InvalidCanvasId(params (string canvasId, string reason)[] multiple)
    {
        var failures = string.Join(", ", multiple.Select(p => $"{p.canvasId}: {p.reason}"));
        var message = $"Errors encountered when parsing painted resources: {failures}.";

        return PresentationResult.Failure(message,
            ModifyCollectionType.InvalidCanvasId, WriteResult.BadRequest);
    }

    public static PresentationResult ErrorMergingPaintedResourcesWithItems(string error)
        => PresentationResult.Failure(error,
            ModifyCollectionType.ErrorMergingPaintedResourcesWithItems, WriteResult.BadRequest);

    public static PresentationResult DuplicateCanvasId(string? canvasId)
        => PresentationResult.Failure($"The canvas ID {canvasId} cannot be a duplicate",
            ModifyCollectionType.DuplicateCanvasId, WriteResult.BadRequest);

    public static PresentationResult IncorrectPublicId()
        => PresentationResult.Failure("publicId incorrect",
            ModifyCollectionType.PublicIdIncorrect, WriteResult.BadRequest);

    public static PresentationResult PaintableAssetError(string error)
        => PresentationResult.Failure(error,
            ModifyCollectionType.PaintableAssetError, WriteResult.BadRequest);

    public static PresentationResult AssetError(AssetException exception)
        => PresentationResult.Failure(exception.Message,
            ModifyCollectionType.AssetError, WriteResult.BadRequest);

    public static PresentationResult CustomerIdDoesNotMatchCaller(string field)
        => PresentationResult.Failure($"The {field} has a customer id that does not match the customer id found on the calling URL",
            ModifyCollectionType.CustomerIdDoesNotMatchCaller, WriteResult.BadRequest);

    public static PresentationResult AssetAdjunctsDoNotMatch(string assetId, string diff)
        => PresentationResult.Failure(
            $"Asset {assetId} is specified multiple times with different adjuncts - diff: {diff}",
            ModifyCollectionType.AssetsAdjunctsDoNotMatch, WriteResult.BadRequest);

    public static PresentationResult ManifestCurrentlyIngesting()
        => PresentationResult.Failure(
            "The manifest is currently being ingested and cannot be modified",
            ModifyCollectionType.ManifestCurrentlyIngesting, WriteResult.Conflict);

    public static PresentationResult AssetsDataDoesNotMatch(string assetId, string diff)
        => PresentationResult.Failure(
            $"Asset {assetId} is specified multiple times, but has conflicting data - diff: {diff}",
            ModifyCollectionType.AssetsDoNotMatch, WriteResult.BadRequest);

    private static string CollectionType(bool isStorageCollection)
    {
        return isStorageCollection ? "Storage" : "IIIF";
    }
}
