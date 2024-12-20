using Models.Database.Collections;

namespace Models.Database.General;

public class Batch
{
    /// <summary>
    /// Id of the batch, coming from the DLCS
    /// </summary>
    public required int Id { get; set; }
    
    /// <summary>
    /// Id of the customer
    /// </summary>
    public int CustomerId { get; set; }
    
    /// <summary>
    /// Status of the batch
    /// </summary>
    public BatchStatus Status { get; set; }
    
    /// <summary>
    /// When the batch was added to the DLCS
    /// </summary>
    public DateTime Submitted { get; set; }
    
    /// <summary>
    /// When the batch was added to the DLCS
    /// </summary>
    public DateTime? Processed { get; set; }
    
    /// <summary>
    /// Id of related manifest
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
