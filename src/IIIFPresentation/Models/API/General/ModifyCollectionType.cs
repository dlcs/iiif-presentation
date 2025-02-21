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
    ErrorCreatingSpace = 11,
    RequiresSpaceHeader = 12,
    CouldNotRetrieveAssetId = 13,
    DlcsException = 14,
    ValidationFailed = 15,
    ParentMustBeStorage = 16,
    Unknown = 1000
}
