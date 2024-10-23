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
    CannotMoveToStorageCollection = 10,
    Unknown = 1000
}