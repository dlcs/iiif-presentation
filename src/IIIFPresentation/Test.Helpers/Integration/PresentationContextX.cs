using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Models.Database.Collections;
using Repository;

namespace Test.Helpers.Integration;

public static class PresentationContextX
{
    public static Guid? GetETag<T>(this PresentationContext db, T obj)
        => obj switch
        {
            Collection c => db.Collections.AsNoTracking().FirstOrDefault(x => x.Id == c.Id && x.CustomerId == c.CustomerId)?.Etag,
            EntityEntry<Collection> c => db.GetETag(c.Entity),
            Manifest m => db.Manifests.AsNoTracking().FirstOrDefault(x => x.Id == m.Id && x.CustomerId == m.CustomerId)?.Etag,
            EntityEntry<Manifest> c => db.GetETag(c.Entity),
            _ => null
        };

    public static Guid? GetETagById(this PresentationContext db, int customerId, string id) =>
        db.Hierarchy.AsNoTracking()
                .Include(x => x.Collection)
                .Include(x => x.Manifest)
                .FirstOrDefault(h => (h.CollectionId == id || h.ManifestId == id) && h.CustomerId == customerId)
            switch
            {
                { Manifest: { } m } => m.Etag,
                { Collection: { } c } => c.Etag,
                _ => null
            };
}
