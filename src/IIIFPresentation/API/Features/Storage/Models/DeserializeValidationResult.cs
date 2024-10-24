using System.Diagnostics.CodeAnalysis;
using IIIF;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Storage.Models;

public class DeserializeValidationResult<T> where T : JsonLdBase
{
    public static DeserializeValidationResult<T> Success(T iiif, string rawRequestBody)
    {
        return new DeserializeValidationResult<T>()
        {
            ConvertedIIIF = iiif,
            RawRequestBody = rawRequestBody
        };
    }
    
    public static DeserializeValidationResult<T> Failure(ActionResult error)
    {
        return new DeserializeValidationResult<T>()
        {
            Error = error
        };
    }
    
    public ActionResult? Error { get; private init; }
    
    [MemberNotNullWhen(returnValue: false, member: nameof(Error))]
    public T? ConvertedIIIF { get; private init; }
    
    [MemberNotNullWhen(returnValue: false, member: nameof(Error))]
    public string RawRequestBody { get; private init; }
}