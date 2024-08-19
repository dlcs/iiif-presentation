namespace Models.API.General;

public class Error
{
    public string Type => "Error";
    
    public string? Description { get; set; }
}