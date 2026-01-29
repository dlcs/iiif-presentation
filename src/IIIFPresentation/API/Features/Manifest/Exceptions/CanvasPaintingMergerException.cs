namespace API.Features.Manifest.Exceptions;

public class CanvasPaintingMergerException : Exception
{
    public CanvasPaintingMergerException(string? message) : base(message)
    {
    }
    
    public CanvasPaintingMergerException(string? expected, string? actual, string id, string? message) : base(message)
    {
        Expected = expected;
        Actual = actual;
        Id = id;
    }

    public CanvasPaintingMergerException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
    
    public CanvasPaintingMergerException(string? expected, string? actual, string? id, string? message, Exception? innerException) :
        base(message, innerException)
    {
        Expected = expected;
        Actual = actual;
        Id = id;
    }
    
    public string? Expected { get; set; }
    
    public string? Actual { get; set; }
    
    public string? Id { get; set; }
}
