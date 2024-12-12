using Models.Database.Collections;

namespace Models.Database.General;

public class Batch
{
    /// <summary>
    /// Id of the batch, coming from the DLCS
    /// </summary>
    public required string Id { get; set; }
    
    /// <summary>
    /// Id of the customer
    /// </summary>
    public int CustomerId { get; set; }
    
    /// <summary>
    /// Status of the batch in comp[letion
    /// </summary>
    public BatchStatus Status { get; set; }
    
    /// <summary>
    /// When the batch was added to the DLCS
    /// </summary>
    public DateTime Submitted { get; set; }
    
    /// <summary>
    /// Id of releated manifest
    /// </summary>
    public required string ManifestId { get; set; }
    
    public Manifest? Manifest { get; set; }
}

public enum BatchStatus
{
    Unknown = 0,
    Ingesting = 1,
    Completed = 2
}
