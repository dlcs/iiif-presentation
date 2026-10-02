namespace Services.TextServices;

/// <summary>
/// Bitmask of services/derivatives a text-services job should produce and expose.
/// Note: Mirrors TextServices.Storage.JobServices — keep values in sync if that enum changes.
/// </summary>
[Flags]
public enum JobServices
{
    None          = 0,
    Search        = 1 << 0,
    Autocomplete  = 1 << 1,
    FullText      = 1 << 2,
    Annotations   = 1 << 3,
    Pdf           = 1 << 4,
    TextAugmented = 1 << 5,
    Figures       = 1 << 6,
    All           = ~0,
}
