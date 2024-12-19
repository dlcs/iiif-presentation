using System.Text.Json.Serialization;

namespace DLCS.Models;

public class AllImages : JsonLdBase
{
    public AllImages(List<string> members)
    {
        Members = members.Select(m => new AllImagesMember(m)).ToList();
        Type = "Collection" ;
    }

    [JsonPropertyOrder(3)]
    [JsonPropertyName("member")]
    public List<AllImagesMember> Members { get; set; }
}

public class AllImagesMember(string id)
{
    public string Id { get; set; } = id;
}


