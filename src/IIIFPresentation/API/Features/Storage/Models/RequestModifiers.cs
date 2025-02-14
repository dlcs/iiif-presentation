namespace API.Features.Storage.Models;

public class RequestModifiers
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string? OrderBy { get; init; }
    public bool Descending { get; init; }
}
 
