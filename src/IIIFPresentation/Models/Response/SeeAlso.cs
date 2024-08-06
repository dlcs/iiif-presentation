namespace Models.Response;

public class SeeAlso
{
    public string Id { get; set; }

    public string Type { get; set; }
    
    public Dictionary<string, List<string>> Label { get; set; }
    
    public List<string> Profile { get; set; }
}