namespace API.Converters;

/// <summary>
/// To construct a response object, you need the protocol and domain of the API itself
/// </summary>
public class UrlRoots
{
    /// <summary>
    /// The base URI for current request - this is the full URI excluding path and query string
    /// </summary>
    public string? BaseUrl { get; set; }
}