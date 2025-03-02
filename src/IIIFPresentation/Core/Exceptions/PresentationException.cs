namespace Core.Exceptions;

public class PresentationException : Exception
{
    public PresentationException()
    {
    }

    public PresentationException(string? message) : base(message)
    {
    }

    public PresentationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}