namespace DLCS.Models;

/// <summary>
/// Represents a IIIF-CS batch of assets or adjuncts
/// </summary>
public class Batch : JsonLdBase
{
    /// <summary>
    /// The date the batch was submitted to DLCS
    /// </summary>
    public DateTime Submitted { get; init; }
    
    /// <summary>
    /// The date the batch was finished by DLCS, if it finished
    /// </summary>
    public DateTime? Finished { get; init; }
    
    /// <summary>
    /// The total number of assets or adjuncts in the batch
    /// </summary>
    public int Count { get; init; }
    
    /// <summary>
    /// The number of assets or adjuncts that have been processed
    /// </summary>
    public int Completed { get; init; }
    
    /// <summary>
    /// The number of assets or adjuncts that have failed processing
    /// </summary>
    public int Errors { get; init; }
}
