namespace Models.Response;

public class PartOf
{
    public string Id { get; set; }

    public PresentationType Type { get; set; }
    
    public Dictionary<string, List<string>> Label { get; set; }
}