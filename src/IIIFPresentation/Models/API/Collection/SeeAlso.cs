namespace Models.API.Collection;

public class SeeAlso
{
    public required string Id { get; set; }

    public PresentationType Type { get; set; }
    
    public Dictionary<string, List<string>>? Label { get; set; }
    
    public List<string>? Profile { get; set; }
}