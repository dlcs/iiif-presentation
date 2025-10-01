namespace Services.Manifests.Exceptions;

public class PaintableAssetException : Exception
{
    public PaintableAssetException()
    {
    }

    public PaintableAssetException(string? message) : base(message)
    {
    }

    public PaintableAssetException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
