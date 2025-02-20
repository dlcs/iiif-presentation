using System.Net;
using Core;

namespace DLCS.Exceptions;

/// <summary>
/// Represents an error interacting with DLCS
/// </summary>
public class DlcsException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    
    public DlcsException(string? message, HttpStatusCode? statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public DlcsException(string? message, Exception? innerException, HttpStatusCode? statusCode) : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
