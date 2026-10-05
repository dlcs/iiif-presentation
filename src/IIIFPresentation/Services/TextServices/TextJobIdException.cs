namespace Services.TextServices;

public class TextJobIdException : Exception
{
    public TextJobIdException(string? message) : base(message)
    {
    }

    public TextJobIdException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
