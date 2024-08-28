using System.Runtime.Serialization;

namespace API.Exceptions;

public class APIException : Exception
{
    public APIException()
    {
    }
    
    public APIException(string? message) : base(message)
    {
    }

    public APIException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public virtual int? StatusCode { get; set; }

    public virtual string? Label { get; set; }
}