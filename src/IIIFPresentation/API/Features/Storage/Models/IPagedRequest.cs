using API.Settings;

namespace API.Features.Storage.Models;

/// <summary>
/// A request that carries raw paging/ordering query values, which can be normalised into
/// <see cref="RequestModifiers"/> via <see cref="IPagedRequestX.GetRequestModifiers"/>.
/// </summary>
public interface IPagedRequest
{
    public int? Page { get; }
    public int? PageSize { get; }
    public string? OrderBy { get; }
    public bool Descending { get; }
}

public static class IPagedRequestX
{
    // This is a very thin wrapper around <see cref="RequestModifiers.Create"/> but keeps consuming code clean.
    public static RequestModifiers GetRequestModifiers(this IPagedRequest pagedRequest, ApiSettings settings)
        => RequestModifiers.Create(pagedRequest, settings);
}
