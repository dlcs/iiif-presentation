namespace Models.DLCS;

public class AssetIdException : Exception
{
    public AssetIdException(string? message) : base(message)
    {
    }
    
    public AssetIdException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
