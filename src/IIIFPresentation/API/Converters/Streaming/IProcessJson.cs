using System.Text.Json;

namespace API.Converters.Streaming;

public interface IProcessJson
{
    object GetInitialState();
    void OnToken(ref Utf8JsonReader reader, Utf8JsonWriter writer, ref object customState);
}