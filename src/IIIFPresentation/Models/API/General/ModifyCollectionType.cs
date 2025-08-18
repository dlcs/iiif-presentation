namespace Models.API.General;

public enum ModifyCollectionType
{
    DuplicateSlugValue = 1,
    UnknownDatabaseSaveError = 2,
    ETagNotAllowed = 3,
    ParentCollectionNotFound = 4,
    ETagNotMatched = 5,
    PossibleCircularReference = 6,
    CannotGenerateUniqueId = 7,
    CannotValidateIIIF = 8,
    ParentMustBeStorageCollection = 9,
    CannotChangeCollectionType = 10,
    DlcsError = 11,
    RequiresSpaceHeader = 12,
    CouldNotRetrieveAssetId = 13,
    DlcsException = 14,
    ValidationFailed = 15,
    CannotDeserialize = 16,
    ParentMustMatchPublicId = 17,
    SlugMustMatchPublicId = 18,
    InvalidCanvasId = 19,
    DuplicateCanvasId = 20,
    ErrorMergingPaintedResourcesWithItems = 21,
    PublicIdIncorrect = 22,
    ManifestCreatedWithItemsCannotBeUpdatedWithAssets = 23,
    ManifestCreatedWithAssetsCannotBeUpdatedWithItems  = 24,
    AssetsDoNotMatch = 25,
    Unknown = 1000
}
