namespace API.Infrastructure.Helpers;

public static class EtagComparer
{
    public static bool IsMatch(Guid storedEtag, string? incomingEtag)
    => incomingEtag is {Length:>0} && Guid.TryParse(incomingEtag.Trim('"'), out var guid) && storedEtag == guid;
}
