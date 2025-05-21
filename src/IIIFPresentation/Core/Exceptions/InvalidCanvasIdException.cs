namespace Core.Exceptions;

public class InvalidCanvasIdException : PresentationException
{
    public string CanvasId { get; }

    public InvalidCanvasIdException(string canvasId) : this(canvasId, $"Canvas Id {canvasId} is not valid")
    {
        CanvasId = canvasId;
    }

    public InvalidCanvasIdException(string canvasId, string? message) : base(message)
    {
        CanvasId = canvasId;
    }

    public InvalidCanvasIdException(string canvasId, string? message, Exception? innerException) : base(message,
        innerException)
    {
        CanvasId = canvasId;
    }
}
