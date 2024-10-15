namespace API.Settings;

public class DlcsSettings
{
    public const string SettingsName = "DLCS";
    
    /// <summary>
    /// URL root of DLCS API 
    /// </summary>
    public required Uri ApiUri { get; set; }
}