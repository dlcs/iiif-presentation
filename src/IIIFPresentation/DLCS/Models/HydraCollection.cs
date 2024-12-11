
using System.Text.Json.Serialization;

namespace DLCS.Models;

public class HydraCollection<T> : JsonLdBase
{
    public HydraCollection(List<T> members)
    {
        Members = members;
        Context = "Collection" ;
    }

    [JsonPropertyName("member")]
    public List<T> Members { get; set; }
}
