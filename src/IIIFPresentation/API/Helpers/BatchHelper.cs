using Core.Helpers;
using Models.Database.Collections;
using Models.Database.General;
using Repository;

namespace API.Helpers;

public static class BatchHelper
{
    /// <summary>
    /// This method adds, but does not save batches to the batches table
    /// </summary>
    public static async Task AddBatchesToDatabase(this List<DLCS.Models.Batch> batches, 
        Manifest manifest, PresentationContext dbContext, CancellationToken cancellationToken = default)
    {
        var dbBatches = batches.Select(b => new Models.Database.General.Batch
        {
            Id = Convert.ToInt32(b.ResourceId!.GetLastPathElement()),
            CustomerId = manifest.CustomerId,
            Submitted = b.Submitted.ToUniversalTime(),
            Status = BatchStatus.Ingesting,
            ManifestId = manifest.Id
        });
        
        await dbContext.Batches.AddRangeAsync(dbBatches, cancellationToken);
    }
}
