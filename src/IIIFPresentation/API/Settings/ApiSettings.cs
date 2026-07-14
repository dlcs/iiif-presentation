using AWS.Settings;
using Core.Web;
using DLCS;
using Services.Manifests.Settings;

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

    /// <summary>
    /// Minimum number of characters required in a search term before a search is run
    /// </summary>
    public int MinSearchLength { get; set; } = 3;

    /// <summary>
    /// Search queries taking longer than this, in milliseconds, are logged as a warning
    /// </summary>
    public int SlowSearchThresholdMs { get; set; } = 1000;
    
    public string? PathBase { get; set; }
    
    /// <summary>
    /// Forces reingestion to always occur
    /// </summary>
    public bool AlwaysReingest { get; set; }

    /// <summary>
    /// The maximum number of historical pipeline jobs returned in a Manifest's "finishedPipelines" property
    /// </summary>
    public int FinishedPipelinesLimit { get; set; } = 20;
    
    public required AWSSettings AWS { get; set; }

    public required DlcsSettings DLCS { get; set; }
    
    public PathSettings? PathSettings { get; set; }
}
