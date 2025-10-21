
using System.Text.Json.Serialization;

namespace DLCS.Models;

public class HydraCollection<T> : JsonLdBase
{
    public HydraCollection(T[] members)
    {
        Members = members;
        Context = "Collection" ;
    }

    [JsonPropertyOrder(3)]
    [JsonPropertyName("member")]
    public T[] Members { get; set; }
    
    [JsonPropertyOrder(10)]
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; init; }

    [JsonPropertyOrder(11)]
    [JsonPropertyName("pageSize")]
    public int? PageSize { get; init; }
    
    [JsonPropertyOrder(12)]
    [JsonPropertyName("view")]
    public PartialCollectionView? View { get; init; }
}

