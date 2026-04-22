namespace DLCS.Models;

public class AdjunctAssetIdentifier
{
    public required string Id { get; set; }

    public required List<string> Adjunct { get; set; }
}
