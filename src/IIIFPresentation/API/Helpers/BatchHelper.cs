using Core.Helpers;
using Models.Database.General;
using Repository;
using Batch = DLCS.Models.Batch;
using DbBatch = Models.Database.General.Batch;

namespace API.Helpers;

public static class BatchHelper
{
    /// <summary>
    /// This method creates <see cref="DbBatch"/> entities from provided DLCS <see cref="Batch"/> records and adds these
    /// to current DB context, without saving
    /// </summary>
    public static async Task<IReadOnlyList<DbBatch>> AddBatchesToDatabase(this List<Batch> batches,
        int customerId, string manifestId, PresentationContext dbContext, DeliverableType deliverableType, 
        CancellationToken cancellationToken = default)
    {
        var dbBatches = batches.Select(b => new DbBatch
        {
            Id = Convert.ToInt32(b.ResourceId!.GetLastPathElement()),
            CustomerId = customerId,
            Submitted = b.Submitted.ToUniversalTime(),
            Processed = b.Finished.HasValue ? DateTime.UtcNow : null,
            Finished = b.Finished?.ToUniversalTime(),
            Status = b.Finished.HasValue ? BatchStatus.Completed : BatchStatus.Ingesting,
            DeliverableType = deliverableType,
            ManifestId = manifestId
        }).ToList();
        
        await dbContext.Batches.AddRangeAsync(dbBatches, cancellationToken);
        return dbBatches;
    }
}
