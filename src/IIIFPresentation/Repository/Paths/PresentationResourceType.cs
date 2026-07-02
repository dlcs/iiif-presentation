namespace Repository.Paths;

/// <summary>
/// Types of resources that the Presentation API is aware of and requires path rewrite
/// rules.
/// </summary>
public static class PresentationResourceType
{
    public const string ManifestPrivate = "ManifestPrivate";
    public const string CollectionPrivate = "CollectionPrivate";
    public const string ResourcePublic = "ResourcePublic";
    public const string Canvas = "Canvas";
    public const string TextServiceJob = "TextServiceJob";
}
