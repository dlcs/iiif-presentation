using System.Diagnostics;
using System.Text.Json;

namespace API.Converters.Streaming;

public abstract class StreamingProcessorImplBase<T> : IProcessJson
{
    protected virtual void OnPropertyName(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WritePropertyName(reader.GetString()!);
    }

    protected virtual void OnString(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteStringValue(reader.GetString());
    }

    protected virtual void OnNumber(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteNumberValue(reader.GetDecimal());
    }

    protected virtual void OnTrue(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteBooleanValue(true);
    }

    protected virtual void OnFalse(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteBooleanValue(false);
    }

    protected virtual void OnNull(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteNullValue();
    }

    protected virtual void OnStartObject(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteStartObject();
    }

    protected virtual void OnEndObject(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteEndObject();
    }

    protected virtual void OnStartArray(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteStartArray();
    }

    protected virtual void OnEndArray(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteEndArray();
    }

    protected virtual void OnNone(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        // Do nothing?
    }

    protected virtual void OnComment(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref T currentState)
    {
        writer.WriteCommentValue(reader.GetComment());
    }


    #region Implementation of IProcessJson

    public abstract object GetInitialState();

    public void OnToken(ref Utf8JsonReader reader, Utf8JsonWriter writer, ref object state)
    {
        var currentStatee = (T) state;
        switch (reader.TokenType)
        {
            case JsonTokenType.PropertyName:
                OnPropertyName(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.String:
                OnString(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.Number:
                OnNumber(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.True:
                OnTrue(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.False:
                OnFalse(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.Null:
                OnNull(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.StartObject:
                OnStartObject(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.EndObject:
                OnEndObject(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.StartArray:
                OnStartArray(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.EndArray:
                OnEndArray(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.None:
                OnNone(ref reader, writer, ref currentStatee);
                break;

            case JsonTokenType.Comment:
                OnComment(ref reader, writer, ref currentStatee);
                break;

            default:
                throw new UnreachableException("Unsupported token type.");
        }
    }

    #endregion
}