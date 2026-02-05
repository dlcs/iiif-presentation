namespace Services.Manifests.Exceptions;

/// <summary>
/// General exception for asset errors
/// </summary>
/// <param name="message"></param>
public class AssetException(string? message, string asset) : Exception(message)
{
    public string Asset { get; } = asset;
}
