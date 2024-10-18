using System.Text.Json;

namespace API.Converters.Streaming;

public class S3StoredJsonProcessor(string requestSlug)
    : StreamingProcessorImplBase<S3StoredJsonProcessor.S3ProcessorCustomState>
{
    private const string IdPropertyName = "id";

    #region Overrides of StreamingProcessorImplBase<S3ProcessorCustomState>

    public override object GetInitialState() => new S3ProcessorCustomState();

    protected override void OnPropertyName(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref S3ProcessorCustomState currentState)
    {
        currentState.PropertyName = reader.GetString()!;
        writer.WritePropertyName(currentState.PropertyName);
    }

    protected override void OnString(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref S3ProcessorCustomState currentState)
    {
        currentState.Depth = reader.CurrentDepth;
        writer.WriteStringValue(ProcessPropertyStringValue(reader.GetString(), ref currentState));
    }

    protected override void OnEndObject(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref S3ProcessorCustomState currentState)
    {
        if (reader.CurrentDepth == 0 && !currentState.IdSet)
        {
            writer.WritePropertyName(IdPropertyName);
            writer.WriteStringValue($"managed:{requestSlug}");
        }

        base.OnEndObject(ref reader, writer, ref currentState);
    }

    #endregion


    /// <summary>
    ///     Performs any operations on JSON string token, using <see cref="S3ProcessorCustomState" />
    ///     provided to determine what, if anything, needs to be done.
    /// </summary>
    /// <param name="v">Value read as string JSON token</param>
    /// <param name="currentState">Any data set during current processing</param>
    /// <returns></returns>
    private string? ProcessPropertyStringValue(string? v,
        ref S3ProcessorCustomState currentState)
    {
        if (currentState.Depth == 1
            && IdPropertyName.Equals(currentState.PropertyName, StringComparison.InvariantCultureIgnoreCase))
        {
            currentState.IdSet = true;

            // Found id in the top-level object
            return $"managed:{requestSlug}";
        }

        return v;
    }

    public class S3ProcessorCustomState
    {
        public string? PropertyName { get; set; }
        public int Depth { get; set; }
        public bool IdSet { get; set; }
    }
}