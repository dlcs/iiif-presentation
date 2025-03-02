using System.Text.Json;

namespace API.Converters.Streaming;

public static class StreamingJsonProcessor
{
    /// <summary>
    ///     Reads <paramref name="input" /> UTF-8 JSON stream, token by token, and writes it to the
    ///     <paramref name="output" /> also as UTF-8 JSON, optionally performing changes
    ///     on the fly
    /// </summary>
    /// <param name="input">UTF-8 JSON source</param>
    /// <param name="output">Processed UTF-8 JSON target</param>
    /// <param name="inputLength">If known, helps to read the stream correctly</param>
    /// <param name="implementation">Handles actual </param>
    /// <param name="log">provide logger into static method</param>
    /// <remarks>
    ///     In C#13 it can be made async, but currently ref structs don't work with async/await.
    /// </remarks>
    public static void ProcessJson(Stream input, Stream output, long? inputLength, IProcessJson implementation,
        ILogger? log = null)
    {
        // Initial buffer size - will auto expand if token/whitespace sequence is bigger than that
        const int bufferSize = 1024;
        var initialSize = inputLength.HasValue ? Math.Min(inputLength.Value, bufferSize) : bufferSize;
        Span<byte> buffer = new byte[initialSize];

        // First read - might be the only, might be nothing (if so, short circuit return)
        input.ReadExactly(buffer);
        var totalRead = (long) buffer.Length;
        
        // Note using - without it the data will be lost due to lack of dispose call
        using var writer = new Utf8JsonWriter(output, new() {Indented = true, SkipValidation = true});

        // Initial reader, setting isFinalBlock to false, as we /might/ have more incoming
        var reader = new Utf8JsonReader(buffer, false, default);

        // Way to keep track of whatever data might be necessary during processing - can be expanded as needed
        var currentState = implementation.GetInitialState();
        
        while (true)
            try
            {
                if (!reader.Read())
                {
                    // Reader was unable to read a full token
                    // If this is final block (stream has ended), then we're done
                    if (reader.IsFinalBlock)
                        return;

                    // If it is not final, we get a new reader over updated
                    // buffer that hopefully will be readable
                    var shouldContinue =
                        GetMoreBytesFromStream(input, ref buffer, ref reader, inputLength, ref totalRead);
                    if (!shouldContinue)
                        return; // when we already read all data
                }
                else
                {
                    // A full JSON token was read - we can now write it to the output
                    // Any specific conversions, transformations or translations happen here
                    implementation.OnToken(ref reader, writer, ref currentState);
                }
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "Error while processing stream");
                throw;
            }
    }

    private static bool GetMoreBytesFromStream(Stream input, ref Span<byte> buffer, ref Utf8JsonReader reader,
        long? inputLength, ref long totalRead)
    {
        var remainingStreamBytes = (int) ((inputLength ?? input.Length) - totalRead);
        if (remainingStreamBytes == 0)
            // we're done as well, nothing left to read
            return false;

        var finalRead = false;
        if (reader.BytesConsumed < buffer.Length)
        {
            // The reader has maybe consumed some, but not all the data in buffer
            // The leftover bytes must be moved to the beginning of the buffer
            // and the rest will be filled with data read from stream

            ReadOnlySpan<byte> leftover = buffer[(int) reader.BytesConsumed..];
            var leftoverBytes = leftover.Length;

            if (leftover.Length == buffer.Length)
            {
                // The reader had entire buffer to work with, but it did not contain
                // a full JSON token - this means we need bigger buffer.
                // to avoid having to re-do changing the size we'll get the "correct lower bound" here
                var newLength = buffer.Length * 2;
                if (newLength >= remainingStreamBytes + leftoverBytes)
                {
                    newLength = remainingStreamBytes + leftoverBytes;
                    finalRead = true;
                }

                var temp = new byte[newLength];

                buffer.CopyTo(temp);
                buffer = temp;
            }
            else if (buffer.Length > leftoverBytes + remainingStreamBytes)
            {
                // Trim buffer to the maximum we can expect from input
                buffer = buffer[..(leftoverBytes + remainingStreamBytes)];
                finalRead = true;
            }

            // Move leftover to the beginning of the buffer. Now we have buffer.Length-leftover.Length space to fill
            leftover.CopyTo(buffer);
            
            // Read to the buffer, starting after leftover bytes
            input.ReadExactly(buffer[leftoverBytes..]);
            totalRead += buffer.Length - leftoverBytes;
        }
        else
        {
            // Previous read used all data in buffer

            // Resize if necessary
            if (remainingStreamBytes < buffer.Length)
            {
                buffer = buffer[..remainingStreamBytes];
                finalRead = true;
            }

            // Read full buffer
            input.ReadExactly(buffer);
            totalRead += buffer.Length;

            // as before, use span to limit the range over buffer, to not read "garbage"
        }

        reader = new(buffer, finalRead, reader.CurrentState);

        return true;
    }
}
