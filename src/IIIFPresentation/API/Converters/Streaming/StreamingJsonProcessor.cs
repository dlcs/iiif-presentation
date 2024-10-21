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
    /// <remarks>
    ///     In C#13 it can be made async, but currently ref structs don't work with async/await.
    /// </remarks>
    public static void ProcessJson(Stream input, Stream output, long? inputLength, IProcessJson implementation)
    {
        // Initial buffer size - will auto expand if token/whitespace sequence is bigger than that
        const int bufferSize = 1024;
        var initialSize = inputLength.HasValue ? Math.Min(inputLength.Value, bufferSize) : bufferSize;
        var buffer = new byte[initialSize];

        // First read - might be the only, might be nothing (if so, short circuit return)
        var bytesRead = input.Read(buffer, 0, buffer.Length);
        if (bytesRead == 0) return;

        // Note using - without it the data will be lost due to lack of dispose call
        using var writer = new Utf8JsonWriter(output, new() {Indented = true, SkipValidation = true});

        // Initial reader, setting isFinalBlock to false, as we /might/ have more incoming
        var reader = new Utf8JsonReader(buffer, false, default);

        // Way to keep track of whatever data might be necessary during processing - can be expanded as needed
        var currentState = implementation.GetInitialState();

        var totalRead = 0L;
        while (true)
            if (!reader.Read())
            {
                // Reader was unable to read a full token
                // If this is final block (stream has ended), then we're done
                if (reader.IsFinalBlock)
                    return;

                // If it is not final, we get a new reader over updated
                // buffer that hopefully will be readable
                var shouldContinue = GetMoreBytesFromStream(input, ref buffer, ref reader, inputLength, ref totalRead);
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

    private static bool GetMoreBytesFromStream(Stream input, ref byte[] buffer, ref Utf8JsonReader reader,
        long? inputLength, ref long totalRead)
    {
        totalRead += reader.BytesConsumed;
        if (inputLength.HasValue && totalRead >= inputLength.Value)
            // We're done
            return false;

        int bytesRead;
        if (reader.BytesConsumed < buffer.Length)
        {
            // The reader has consumed some, but not all the data in buffer
            // The leftover bytes must be moved to the beginning of the buffer
            // and the rest will be filled with data read from stream

            ReadOnlySpan<byte> leftover = buffer.AsSpan((int) reader.BytesConsumed);
            var leftoverBytes = leftover.Length;
            if (leftover.Length == buffer.Length)
                // The reader had entire buffer to work with, but it did not contain
                // a full JSON token - this means we need bigger buffer.
                Array.Resize(ref buffer, buffer.Length * 2);

            leftover.CopyTo(buffer);
            // Read to the buffer, starting after leftover bytes
            bytesRead = input.Read(buffer.AsSpan(leftover.Length));

            // After above call the buffer is:
            // [leftover_bytes, read_bytes, 0..N bytes from prev read]
            // If we know the input length, then if the unread bytes (leftover + read) with
            // the previously read sum to the expected length, we're done
            if (
                (inputLength.HasValue && totalRead + leftoverBytes + bytesRead >= inputLength.Value)
                || (!inputLength.HasValue && bytesRead < buffer.Length - leftoverBytes))
            {
                // Set new reader over the read span (as the remainder of buffer can contain previous read bytes)
                reader = new(buffer.AsSpan(0, bytesRead + leftoverBytes), true, reader.CurrentState);
                return true;
            }
        }
        else
        {
            // Previous read used all data in buffer, we can read to start of buffer
            bytesRead = input.Read(buffer);

            // Same as above - if we know the length, and we just read it all
            // or if we don't know the length and the data "stopped", we end
            if ((inputLength.HasValue && totalRead + bytesRead >= inputLength.Value)
                || (!inputLength.HasValue && bytesRead < buffer.Length))
            {
                // as before, use span to limit the range over buffer, to not read "garbage"
                reader = new(buffer.AsSpan(0, bytesRead), true, reader.CurrentState);
                return true;
            }
        }

        // If we're here then it is NOT a final bit:
        reader = new(buffer, false, reader.CurrentState);
        return true;
    }
}

