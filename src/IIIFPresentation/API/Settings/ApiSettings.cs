namespace API.Settings;

public class ApiSettings
{
    /// <summary>
    ///     The base URI for image services and other public-facing resources
    /// </summary>
    public Uri ResourceRoot { get; set; }

    /// <summary>
    /// Page size for paged collections
    /// </summary>
    public int PageSize { get; set; } = 100;
    
    public string PathBase { get; set; }
}