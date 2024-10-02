namespace Models.API.General;

public enum DeleteCollectionType
{
    CannotDeleteRootCollection = 1,
    CollectionNotEmpty = 2,
    Unknown = 3
}

public enum ModifyCollectionType
{
    DuplicateSlugValue = 1,
    UnknownDatabaseSaveError = 2,
    ETagNotAllowed = 3,
    ParentCollectionNotFound = 4,
    ETagNotMatched = 5,
    PossibleCircularReference = 6,
    CannotGenerateUniqueId = 7,
}