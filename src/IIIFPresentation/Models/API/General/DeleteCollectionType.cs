namespace Models.API.General;

public enum DeleteResourceType
{
    CannotDeleteRootCollection = 1,
    CollectionNotEmpty = 2,
    EtagNotMatching = 3,
    Unknown = 3
}
