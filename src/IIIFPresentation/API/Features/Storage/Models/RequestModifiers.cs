using API.Settings;

namespace API.Features.Storage.Models;

/// <summary>
/// Class that contains the normalised values of a <see cref="IPagedRequest"/>, used to modify request results
/// </summary>
public class RequestModifiers
{
    public int Page { get; private init; }
    public int PageSize { get; private init; }
    public string? OrderBy { get; private init; }
    public bool Descending { get; private init; }

    public string? GetOrderByParameter() => OrderBy != null
        ? $"{(Descending ? "orderByDescending" : "orderBy")}={OrderBy}"
        : null;

    /// <summary>
    /// Create an instances from a <see cref="IPagedRequest"/>, clamping paging values if required
    /// </summary>
    public static RequestModifiers Create(IPagedRequest pagedRequest, ApiSettings settings)
    {
        var normalisedPageSize = pagedRequest.PageSize is null or <= 0
            ? settings.PageSize
            : Math.Min(pagedRequest.PageSize.Value, settings.MaxPageSize);

        var normalisedPage = pagedRequest.Page is null or <= 0 ? 1 : pagedRequest.Page.Value;

        return new RequestModifiers
        {
            Descending = pagedRequest.Descending,
            OrderBy = pagedRequest.OrderBy,
            Page = normalisedPage,
            PageSize = normalisedPageSize
        };
    }
}
 
