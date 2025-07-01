using System.Collections.Immutable;
using Microsoft.Extensions.Primitives;

namespace API.Infrastructure.Helpers;

public static class HeadersStringValuesX
{
    public static IImmutableSet<Guid> AsETagValues(this StringValues values)
        => values
            .OfType<string>()
            .Select(x => x.Trim('"'))
            .Select(x => Guid.TryParse(x, out var guid) ? guid : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToImmutableHashSet();
}
