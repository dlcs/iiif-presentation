namespace API.Features.Manifest.Exceptions;

public class CanvasPaintingMergerException : Exception
{
    public CanvasPaintingMergerException(string? message) : base(message)
    {
    }
    
    public CanvasPaintingMergerException(string? expected, string? actual, Uri canvasOriginalId, string? message) : base(message)
    {
        Expected = expected;
        Actual = actual;
        CanvasOriginalId = canvasOriginalId;
    }

    public CanvasPaintingMergerException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
    
    public CanvasPaintingMergerException(string? expected, string? actual, Uri canvasOriginalId, string? message, Exception? innerException) :
        base(message, innerException)
    {
        Expected = expected;
        Actual = actual;
        CanvasOriginalId = canvasOriginalId;
    }
    
    public string? Expected { get; set; }
    
    public string? Actual { get; set; }
    
    public Uri? CanvasOriginalId { get; set; }
}
