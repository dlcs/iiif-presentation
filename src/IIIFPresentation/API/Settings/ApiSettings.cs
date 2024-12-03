using AWS.Settings;
using DLCS;

namespace API.Settings;

public class ApiSettings
{
    /// <summary>
    /// Page size for paged collections
    /// </summary>
    public int PageSize { get; set; } = 100;
    
    /// <summary>
    /// The maximum size of a page
    /// </summary>
    public int MaxPageSize { get; set; } = 1000;
    
    public string? PathBase { get; set; }
    
    public required AWSSettings AWS { get; set; }

    public required DlcsSettings DLCS { get; set; }
}