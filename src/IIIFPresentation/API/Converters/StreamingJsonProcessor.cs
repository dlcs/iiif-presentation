using System.Text.Json;

namespace API.Converters;

public class StreamingJsonProcessor
{
    public static void ProcessJson(Stream input, Stream output, long? inputLength)
    {
        // Initial buffer size - will auto expand if token/whitespace sequence is bigger than that
        const int bufferSize = 1024;
        var buffer = new byte[bufferSize];

        // First read - might be the only, might be nothing (if so, short circuit return)
        var bytesRead = input.Read(buffer, 0, bufferSize);
        if (bytesRead == 0) return;

        // Note using - without it the data will be lost due to lack of dispose call
        using var writer = new Utf8JsonWriter(output, new() {Indented = true, SkipValidation = true});

        // Initial reader, setting isFinalBlock to false, as we /might/ have more incoming
        var reader = new Utf8JsonReader(buffer, false, default);

        // Way to keep track of whatever data might be necessary during processing - can be expanded as needed
        var currentState = new CustomState();

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
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propertyName = reader.GetString()!;
                        writer.WritePropertyName(propertyName);

                        // Save property name to custom state, as the next iteration we'll
                        // only know that there is a value being read
                        currentState.PropertyName = propertyName;
                        break;

                    case JsonTokenType.String:
                        writer.WriteStringValue(ProcessPropertyStringValue(reader.GetString(), ref currentState));
                        break;

                    case JsonTokenType.Number:
                        writer.WriteNumberValue(reader.GetDecimal());
                        break;

                    case JsonTokenType.True:
                        writer.WriteBooleanValue(true);
                        break;

                    case JsonTokenType.False:
                        writer.WriteBooleanValue(false);
                        break;

                    case JsonTokenType.Null:
                        writer.WriteNullValue();
                        break;

                    case JsonTokenType.StartObject:
                        writer.WriteStartObject();
                        break;

                    case JsonTokenType.EndObject:
                        writer.WriteEndObject();
                        break;

                    case JsonTokenType.StartArray:
                        writer.WriteStartArray();
                        break;

                    case JsonTokenType.EndArray:
                        writer.WriteEndArray();
                        break;

                    case JsonTokenType.None:
                        // Do nothing?
                        break;

                    case JsonTokenType.Comment:
                        writer.WriteCommentValue(reader.GetComment());
                        break;

                    default:
                        throw new InvalidOperationException("Unsupported token type.");
                }
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

    /// <summary>
    ///     Performs any operations on JSON string token, using <see cref="CustomState" />
    ///     provided to determine what, if anything, needs to be done.
    /// </summary>
    /// <param name="v">Value read as string JSON token</param>
    /// <param name="customState">Any data set during current processing</param>
    /// <returns></returns>
    private static string? ProcessPropertyStringValue(string? v, ref CustomState customState)
        => customState.PropertyName switch
        {
            "id" => v?.Replace("slf.digirati.io", "localhost"),
            _ => v
        };

    private ref struct CustomState
    {
        public string? PropertyName { get; set; }
    }
}