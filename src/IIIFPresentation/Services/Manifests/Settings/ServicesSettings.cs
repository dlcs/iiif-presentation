namespace Services.Manifests.Settings;

public class ServicesSettings
{
    public const string SettingsName = "ServicesSettings";

    public char[] ProhibitedCharacters { get; set; } = ['/', '=', ','];

    public string ProhibitedCharactersDisplay =>
        string.Join(", ", ProhibitedCharacters.Select(p => $"'{p}'"));
}
