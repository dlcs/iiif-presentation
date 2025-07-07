using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace DLCS.Models;

public class BulkPatchAssets
{
    [JsonPropertyName("field")]
    public required string Field { get; set; }
    
    [JsonPropertyName("operation")]
    public OperationType Operation { get; set; }
    
    [JsonPropertyName("value")]
    public required List<string>? Value { get; set; }
    
    [JsonPropertyName("member")]
    public required List<IdentifierOnly> Members { get; set; }
}

public enum OperationType
{
    [EnumMember(Value = "unknown")]
    Unknown = 0,
    [EnumMember(Value = "add")]
    Add,
    [EnumMember(Value = "remove")]
    Remove,
    [EnumMember(Value = "replace")]
    Replace,
}

public class IdentifierOnly(string id)
{
    public string Id { get; set; } = id;
}
