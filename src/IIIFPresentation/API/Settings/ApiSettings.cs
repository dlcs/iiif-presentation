using AWS.Settings;
using Core.Web;
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
    
    /// <summary>
    /// Whether painted resources should be ignored when there are also items
    /// </summary>
    public bool IgnorePaintedResourcesWithItems { get; set; }
    
    public bool AlwaysReingest { get; set; }
    
    public required AWSSettings AWS { get; set; }

    public required DlcsSettings DLCS { get; set; }
    
    public TypedPathTemplateOptions PathRules { get; set; } = new ();
}
