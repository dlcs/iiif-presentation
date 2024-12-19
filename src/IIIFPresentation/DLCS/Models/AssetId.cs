namespace DLCS.Models;


/// <summary>
/// A record that represents an identifier for a DLCS Asset.
/// </summary>
public class AssetId
{
    /// <summary>Id of customer</summary>
    public int Customer { get; }

    /// <summary>Id of space</summary>
    public int Space { get; }

    /// <summary>Id of asset</summary>
    public string Asset { get; }
    
    /// <summary>
    /// A record that represents an identifier for a DLCS Asset.
    /// </summary>
    public AssetId(int customer, int space, string asset)
    {
        Customer = customer;
        Space = space;
        Asset = asset;
    }
    
    public override string ToString() => $"{Customer}/{Space}/{Asset}";

    /// <summary>
    /// Create a new AssetId from string in format customer/space/image
    /// </summary>
    public static AssetId FromString(string assetImageId)
    {
        var parts = assetImageId.Split("/", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new ArgumentException($"AssetId '{assetImageId}' is invalid. Must be in format customer/space/asset");
        }

        try
        {
            return new AssetId(int.Parse(parts[0]), int.Parse(parts[1]), parts[2]);
        }
        catch (FormatException fmEx)
        {
            throw new ArgumentException($"AssetId '{assetImageId}' is invalid. Must be in format customer/space/asset",
                fmEx);
        }
    }
}
