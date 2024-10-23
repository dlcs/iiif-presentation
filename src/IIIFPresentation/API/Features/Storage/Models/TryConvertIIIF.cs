using System.Diagnostics.CodeAnalysis;
using IIIF;

namespace API.Features.Storage.Models;

public class TryConvertIIIF<T> where T : JsonLdBase
{
    public static TryConvertIIIF<T> Success(T iiif)
    {
        return new TryConvertIIIF<T>()
        {
            Error = false,
            ConvertedIIIF = iiif
        };
    }
    
    public static TryConvertIIIF<T> Failure()
    {
        return new TryConvertIIIF<T>()
        {
            Error = true
        };
    }
    
    public bool Error { get; private init; }
    
    [MemberNotNullWhen(returnValue: false, member: nameof(Error))]
    public T? ConvertedIIIF { get; private init; }
}