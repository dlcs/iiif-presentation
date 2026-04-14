namespace Models.API.General;

public enum DeleteResourceErrorType
{
    CannotDeleteRootCollection = 1,
    CollectionNotEmpty = 2,
    EtagNotMatching = 3,
    NotFound = 4,
    Unknown = 5
}
