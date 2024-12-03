namespace DLCS.Models;

public class Space : JsonldBase
{
    public int? Id { get; set; }
    public required string Name { get; set; }
}