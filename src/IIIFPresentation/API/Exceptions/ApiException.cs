﻿using System.Runtime.Serialization;

namespace API.Exceptions;

public class APIException : Exception
{
    public APIException()
    {
    }

    protected APIException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public APIException(string? message) : base(message)
    {
    }

    public APIException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public virtual int? StatusCode { get; set; }

    public virtual string Label { get; set; }
}