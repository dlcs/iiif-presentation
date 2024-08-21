namespace Models.API.Collection;

public class Item
{
    public required string Id { get; set; }
    
    public PresentationType Type { get; set; }
    
    public Dictionary<string, List<string>>? Label { get; set; }
}