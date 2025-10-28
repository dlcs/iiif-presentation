using IIIF.Presentation.V3.Strings;

namespace API.Features.Storage.Helpers;

public static class LanguageMapX
{
    public static bool CheckEquality(this LanguageMap? first, LanguageMap? second)
    {
        return first?.Any(entry =>
        {
            List<string>? languageMapValues = null;
            second?.TryGetValue(entry.Key, out languageMapValues);
            if (languageMapValues == null) return false;

            return languageMapValues.SequenceEqual(entry.Value);
        }) ?? false;
    }
}
