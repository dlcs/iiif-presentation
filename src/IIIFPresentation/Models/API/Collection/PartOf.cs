namespace Models.API.Collection;

public class PartOf
{
    public required string Id { get; set; }

    public PresentationType Type { get; set; }
    
    public Dictionary<string, List<string>>? Label { get; set; }
}