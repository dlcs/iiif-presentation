using System.Diagnostics.CodeAnalysis;
using IIIF;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Storage.Models;

public class DeserializeValidationResult<T> where T : JsonLdBase
{
    public static DeserializeValidationResult<T> Success(T iiif, string rawRequestBody) =>
        new()
        {
            ConvertedIIIF = iiif,
            RawRequestBody = rawRequestBody
        };

    public static DeserializeValidationResult<T> Failure(ActionResult error) =>
        new()
        {
            Error = error
        };

    [MemberNotNullWhen(returnValue: false, member: nameof(ConvertedIIIF))]
    [MemberNotNullWhen(returnValue: false, member: nameof(RawRequestBody))]
    [MemberNotNullWhen(returnValue: true, member: nameof(Error))]
    public bool HasError => Error != null;
    
    public ActionResult? Error { get; private init; }
    
    public T? ConvertedIIIF { get; private init; }
    
    public string? RawRequestBody { get; private init; }
}
