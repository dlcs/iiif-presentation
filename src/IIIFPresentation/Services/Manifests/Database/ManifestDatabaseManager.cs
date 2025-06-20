using Models.Database.General;

namespace Services.Manifests.Database;

public class ManifestDatabaseManager : IManifestDatabaseManager
{
    public void CompleteBatch(Batch batch, DateTime finished, bool finalBatch)
    {
        var processed = DateTime.UtcNow;
        
        batch.Processed = processed;
        batch.Finished = finished;
        batch.Status = BatchStatus.Completed;

        if (finalBatch)
        {
            batch.Manifest!.LastProcessed = processed;
        }
    }
}


public interface IManifestDatabaseManager
{
    public void CompleteBatch(Batch batch, DateTime finished, bool finalBatch);
}
