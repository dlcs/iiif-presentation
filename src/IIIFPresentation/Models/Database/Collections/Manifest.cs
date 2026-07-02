using System.Globalization;
using IIIF.Presentation.V3.Strings;
using Models.Database.General;

namespace Models.Database.Collections;

public class Manifest : IHierarchyResource
{
    public required string Id { get; set; }
    
    /// <summary>
    /// The customer identifier
    /// </summary>
    public required int CustomerId { get; set; }
    
    /// <summary>
    /// Created date/time
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Last modified date/time
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// Who created this Manifest
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Who last committed a change to this Manifest
    /// </summary>
    public string? ModifiedBy { get; set; }
    
    /// <summary>
    /// Manifest label
    /// </summary>
    public LanguageMap? Label { get; set; }
    
    /// <summary>
    /// Optional DLCS space id associated with this manifest
    /// </summary>
    public int? SpaceId { get; set; }
    
    public List<Hierarchy>? Hierarchy { get; set; }
    public List<CanvasPainting>? CanvasPaintings { get; set; }
    
    public List<Batch>? Batches { get; set; }

    public List<PipelineJob>? PipelineJobs { get; set; }
    
    /// <summary>
    /// A timestamp denoting when this batch was last processed into a user viewable format
    /// </summary>
    public DateTime? LastProcessed { get; set; }

    public Guid Etag { get; set; }
}

public static class ManifestX
{
    /// <summary>
    /// Get the default space name for manifests Dlcs space
    /// </summary>
    public static string GetDefaultSpaceName(string manifestId)
        => $"For manifest {manifestId} - {DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Whether the manifest has an active DLCS batch still ingesting assets.
    /// </summary>
    public static bool IsIngesting(this Manifest? manifest)
        => manifest?.Batches?.Any(m => m.Status == BatchStatus.Ingesting) ?? false;

    /// <summary>
    /// Whether a text-services pipeline job is pending for this manifest.
    /// </summary>
    public static bool HasPendingPipelineJob(this Manifest? manifest)
        => manifest?.PipelineJobs?.Any(p => p.Status is PipelineJobStatus.NotSubmitted or PipelineJobStatus.Waiting) ?? false;

    /// <summary>
    /// Whether the manifest has any outstanding background work that must complete before it reaches its final state.
    /// Covers both DLCS batch ingestion and text-services pipeline jobs.
    /// </summary>
    public static bool HasFurtherWork(this Manifest? manifest)
        => manifest.IsIngesting() || manifest.HasPendingPipelineJob();
}
