
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
}
