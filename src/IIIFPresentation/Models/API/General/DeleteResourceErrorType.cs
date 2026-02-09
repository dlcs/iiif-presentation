namespace Models.API.General;

/// <summary>
/// Various errors that occur from deletes
/// </summary>
/// <remarks>This is output in publicly available paths</remarks>
public enum DeleteResourceErrorType
{
    CannotDeleteRootCollection = 1,
    CollectionNotEmpty = 2,
    EtagNotMatching = 3,
    NotFound = 4,
    Unknown = 5
}
