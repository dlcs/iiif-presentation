namespace Models.API.General;

public class Error
{
    public string Type => "Error";
    
    public string? Detail { get; set; }
    
    public int Status { get; set; }
}