namespace Services.Manifests.Settings;

public class ServicesSettings
{
    public const string SettingsName = "ServicesSettings";

    // NOTE: this is used by both adjuncts and canvas id's - if a change is made for 1, consider that it will affect both
    public char[] ProhibitedCharacters { get; set; } = ['/', '=', ','];

    public string ProhibitedCharactersDisplay =>
        string.Join(", ", ProhibitedCharacters.Select(p => $"'{p}'"));
}
