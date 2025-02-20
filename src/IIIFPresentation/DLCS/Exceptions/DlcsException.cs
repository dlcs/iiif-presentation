using Core;

namespace DLCS.Exceptions;

/// <summary>
/// Represents an error interacting with DLCS
/// </summary>
public class DlcsException : Exception
{
    WriteResult writeResult;
    
    public DlcsException(string? message) : base(message)
    {
        writeResult = WriteResult.Error;
    }

    public DlcsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
