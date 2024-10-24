using System.Diagnostics.CodeAnalysis;
using IIIF;

namespace API.Features.Storage.Models;

public class TryConvertIIIFResult<T> where T : JsonLdBase
{
    public static TryConvertIIIFResult<T> Success(T iiif)
    {
        return new TryConvertIIIFResult<T>()
        {
            Error = false,
            ConvertedIIIF = iiif
        };
    }
    
    public static TryConvertIIIFResult<T> Failure()
    {
        return new TryConvertIIIFResult<T>()
        {
            Error = true
        };
    }
    
    public bool Error { get; private init; }
    
    [MemberNotNullWhen(returnValue: false, member: nameof(Error))]
    public T? ConvertedIIIF { get; private init; }
}