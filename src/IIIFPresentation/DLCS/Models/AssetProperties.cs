namespace DLCS.Models;

/// <summary>
/// Asset is generally a pass-through object and isn't strongly typed. However, we do access some properties of it so
/// this class maintains a list of accessed properties
/// </summary>
public static class AssetProperties
{
    public const string Space = "space";
    public const string Id = "id";
    public const string Error = "error";
    public const string FullId = "@id";
    public const string Ingesting = "ingesting";
}
