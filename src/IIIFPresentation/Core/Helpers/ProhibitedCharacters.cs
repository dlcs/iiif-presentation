namespace Core.Helpers;

public static class ProhibitedCharacters
{
    // NOTE: this is shared by adjuncts and canvas id - if modified make sure this is ok for both
    public static readonly char[] Characters = ['/', '=', ','];

    public static readonly string Display =
        string.Join(", ", Characters.Select(p => $"'{p}'"));
}
