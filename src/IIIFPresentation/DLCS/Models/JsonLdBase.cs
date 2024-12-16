using System.Text.Json.Serialization;

namespace DLCS.Models;

/// <summary>
/// Base class for all JSON-LD objects.
/// </summary>
public abstract class JsonLdBase
{
    [JsonPropertyName("@context")]
    [JsonPropertyOrder(0)]
    public string? Context { get; set; } = null;

    [JsonPropertyName("@id")]
    [JsonPropertyOrder(1)]
    public string? ResourceId { get; set; }

    [JsonPropertyName("@type")]
    [JsonPropertyOrder(2)]
    public string? Type { get; set; }
}
