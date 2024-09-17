namespace Models.API.General;

public enum DeleteCollectionType
{
    CannotDeleteRootCollection = 1,
    CollectionNotEmpty = 2,
    Unknown = 3
}