using Core.Helpers;
using Models.Database.General;
using Repository;
using Batch = DLCS.Models.Batch;

namespace API.Helpers;

public static class BatchHelper
{
    /// <summary>
    /// This method adds, but does not save batches to the batches table
    /// </summary>
    public static async Task AddBatchesToDatabase(this List<Batch> batches,
        int customerId, string manifestId, PresentationContext dbContext, DeliverableType deliverableType = DeliverableType.Asset, 
        CancellationToken cancellationToken = default)
    {
        var dbBatches = batches.Select(b => new Models.Database.General.Batch
        {
            Id = Convert.ToInt32(b.ResourceId!.GetLastPathElement()),
            CustomerId = customerId,
            Submitted = b.Submitted.ToUniversalTime(),
            Processed = deliverableType == DeliverableType.Asset ? null : DateTime.UtcNow,
            Finished = deliverableType == DeliverableType.Asset ? null : DateTime.UtcNow,
            Status = deliverableType == DeliverableType.Asset ? BatchStatus.Ingesting : BatchStatus.Completed,
            DeliverableType = deliverableType,
            ManifestId = manifestId
        });
        
        await dbContext.Batches.AddRangeAsync(dbBatches, cancellationToken);
    }
}
