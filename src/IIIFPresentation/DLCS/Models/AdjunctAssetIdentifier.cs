namespace DLCS.Models;

/// <summary>
/// Class used to allow for IIIF-CS calls that need just a list of adjunct id's attached to an asset
/// <remarks>Used for things like deleting unneeded adjuncts</remarks>
/// </summary>
public class AdjunctAssetIdentifier
{
    /// <summary>
    /// This is the string representation of an asset id i.e.: "{customer}/{space}/{id}"
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// This is a list of adjunct id's
    /// </summary>
    public required List<string> Adjunct { get; set; }
}
