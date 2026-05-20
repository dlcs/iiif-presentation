using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Core.Streams;

public static class StreamX
{
    /// <summary>
    /// Check if Stream is null or Stream.Null 
    /// </summary>
    /// <param name="stream">Stream to check</param>
    /// <returns>True if stream is null</returns>
    public static bool IsNull([NotNullWhen(false)]this Stream? stream)
        => stream == null || stream == Stream.Null;
    
    public static async Task<string> ReadStreamAsStringAsync(
        this Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}
