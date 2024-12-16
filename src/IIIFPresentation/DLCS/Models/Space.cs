namespace DLCS.Models;

public class Space : JsonLdBase
{
    public int? Id { get; set; }
    public required string Name { get; set; }
}