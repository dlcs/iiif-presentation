namespace Test.Helpers.Helpers;

public static class StringTestX
{
    public static string VaryCase(this string s)
    {
        var a = s.ToCharArray();
        for (var i = 0; i < a.Length; i++)
        {
            var c = a[i];
            if (!char.IsLetter(c))
                continue;

            a[i] = char.IsLower(c) ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
        }

        return new(a);
    }
}