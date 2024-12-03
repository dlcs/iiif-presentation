namespace DLCS.Exceptions;

/// <summary>
/// Represents an error interacting with DLCS
/// </summary>
public class DlcsException : Exception
{
    public DlcsException(string? message) : base(message)
    {
    }

    public DlcsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}