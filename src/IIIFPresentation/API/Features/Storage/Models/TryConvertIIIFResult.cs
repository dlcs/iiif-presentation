using System.Diagnostics.CodeAnalysis;
using IIIF;

namespace API.Features.Storage.Models;

public class TryConvertIIIFResult<T> where T : JsonLdBase
{
    public static TryConvertIIIFResult<T> Success(T iiif) =>
        new()
        {
            Error = false,
            ConvertedIIIF = iiif
        };

    public static TryConvertIIIFResult<T> Failure() =>
        new()
        {
            Error = true
        };

    [MemberNotNullWhen(returnValue: false, member: nameof(ConvertedIIIF))]
    public bool Error { get; private init; }
    
    public T? ConvertedIIIF { get; private init; }
}
