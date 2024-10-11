namespace API.Settings;

public class DlcsSettings
{
    public const string SettingsName = "Dlcs";
    
    /// <summary>
    /// URL root of DLCS API 
    /// </summary>
    public Uri ApiUri { get; set; }
}