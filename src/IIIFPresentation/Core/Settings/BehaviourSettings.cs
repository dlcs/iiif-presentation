namespace Core.Settings;

public class BehaviourSettings
{
    /// <summary>
    /// Marks a point in time since the payload storing functionality was deployed.
    /// This serves as a way to limit other functionality that depends on storing payloads to items that were
    /// created afterwards.
    /// null value means all items are expected to have been created after that functionality was deployed.
    /// </summary>
    public DateTimeOffset? StoresPayloadsSince { get; set; }

    /// <summary>
    /// Returns true if the "Store payload" functionality was available at specified date.
    /// </summary>
    public bool ShouldHaveStoredOriginal(DateTime created)
        => !StoresPayloadsSince.HasValue || created >= StoresPayloadsSince;
}
