namespace Core.Helpers;

public static class ProhibitedCharacters
{
    public static readonly char[] Characters = ['/', '=', ','];

    public static readonly string Display =
        string.Join(", ", Characters.Select(p => $"'{p}'"));
}
