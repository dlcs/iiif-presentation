namespace Models.DLCS;


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
            throw new ArgumentException(
                $"AssetId '{assetImageId}' is invalid. Must be in format customer/space/asset");
        }

        try
        {
            return new AssetId(int.Parse(parts[0]), int.Parse(parts[1]), parts[2]);
        }
        catch (FormatException fmEx)
        {
            throw new ArgumentException(
                $"AssetId '{assetImageId}' is invalid. Must be in format customer/space/asset",
                fmEx);
        }
    }
    
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        var asset = (AssetId)obj;
        return asset.ToString() == this.ToString();
    }

    public static bool operator ==(AssetId? assetId1, AssetId? assetId2)
    {
        if (assetId1 is null)
        {
            return assetId2 is null;
        }
        
        if (assetId2 is null)
        {
            return false;
        }
        
        return assetId1.Equals(assetId2);
    }
    
    public static bool operator !=(AssetId? assetId1, AssetId? assetId2) 
        => !(assetId1 == assetId2);
    
    public override int GetHashCode() => HashCode.Combine(Customer, Space, Asset);
}
