using Core;

namespace API.Features.Storage.Models;

public class DeleteStorageCollection
{
    public string? Error { get; set; }
    
    public decimal? Status { get; set; }

    private readonly DeleteResult result;
    
    public string Title => SpaceSeparated(result);
    
    public string? TraceId { get; }

    public DeleteStorageCollection(string error, decimal status, string? trace, DeleteResult result = DeleteResult.Error)
    {
            Error = error;
            Status = status;
            TraceId = trace;
            result = result;
    }
    
    public DeleteStorageCollection(DeleteResult result)
    {
        this.result = result;
    }
    
    private string SpaceSeparated(DeleteResult result)
    {
        return string.Concat(result.ToString().Select(x => Char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
    }
}