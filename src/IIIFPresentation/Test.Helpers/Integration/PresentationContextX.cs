using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Models.Database.Collections;
using Models.Database.General;
using Repository;

namespace Test.Helpers.Integration;

public static class PresentationContextX
{
    public static Guid? GetETag<T>(this PresentationContext db, T obj)
        => obj switch
        {
            Collection c => db.Collections.AsNoTracking().FirstOrDefault(x => x.Id == c.Id)?.Etag,
            EntityEntry<Collection> c => db.GetETag(c.Entity),
            Manifest m => db.Manifests.AsNoTracking().FirstOrDefault(x => x.Id == m.Id)?.Etag,
            EntityEntry<Manifest> c => db.GetETag(c.Entity),
            _ => null
        };
    
    public static Guid? GetETag(this PresentationContext db, string id, int customerId, ResourceType resourceType)
        => resourceType switch
        {
            ResourceType.IIIFManifest => db.Manifests.AsNoTracking().FirstOrDefault(x => x.Id == id)?.Etag,
            _ => db.Collections.AsNoTracking().FirstOrDefault(x => x.Id == id)?.Etag
        };
}
