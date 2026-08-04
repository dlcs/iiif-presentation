namespace Services.Manifests.Settings;

public class ServicesSettings
{
    public const string SettingsName = "ServicesSettings";

    // NOTE: this is used by both adjuncts and canvas id's - if a change is made for 1, consider that it will affect both
    public string[] ProhibitedCanvasIdCharacters { get; set; } = ["/", "=", ","];

    public string ProhibitedCanvasIdCharactersDisplay =>
        string.Join(", ", ProhibitedCanvasIdCharacters.Select(p => $"'{p}'"));
    
    public string[] ProhibitedAdjunctIdCharacters { get; set; } = ["/", "\\"];

    public string ProhibitedAdjunctIdCharactersDisplay =>
        string.Join(", ", ProhibitedAdjunctIdCharacters.Select(p => $"'{p}'"));

    public string[] ProhibitedSlugCharacters { get; set; } = ["/"];

    public string ProhibitedSlugCharactersDisplay =>
        string.Join(", ", ProhibitedSlugCharacters.Select(p => $"'{p}'"));
}
