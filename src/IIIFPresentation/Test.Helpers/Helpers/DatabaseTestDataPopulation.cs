using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Models.Database.Collections;
using Models.Database.General;

namespace Test.Helpers.Helpers;

/// <summary>
/// Helpers for creating resources for test
/// </summary>
public static class DatabaseTestDataPopulation
{
    public static ValueTask<EntityEntry<Manifest>> AddTestManifest(this DbSet<Manifest> manifests,
        [CallerMemberName] string id = "", int customer = 1, DateTime? createdDate = null, string? slug = null,
        string parent = "root")
    {
        createdDate ??= DateTime.UtcNow;
        return manifests.AddAsync(new Manifest
        {
            Id = id,
            CustomerId = customer,
            CreatedBy = "Admin",
            Created = createdDate.Value,
            Modified = createdDate.Value,
            Hierarchy =
            [
                new Hierarchy
                {
                    Canonical = true,
                    Slug = slug ?? $"sm_{id}",
                    Parent = parent,
                    Type = ResourceType.IIIFManifest
                }
            ]
        });
    }
    
    public static ValueTask<EntityEntry<Collection>> AddTestCollection(this DbSet<Collection> collections,
        [CallerMemberName] string id = "", int customer = 1, DateTime? createdDate = null, string? slug = null,
        string parent = "root", bool isStorage = true, bool isPublic = true)
    {
        createdDate ??= DateTime.UtcNow;
        return collections.AddAsync(new Collection
        {
            Id = id,
            CustomerId = customer,
            CreatedBy = "Admin",
            Created = createdDate.Value,
            Modified = createdDate.Value,
            IsStorageCollection = isStorage,
            IsPublic = isPublic,
            Hierarchy =
            [
                new Hierarchy
                {
                    Canonical = true,
                    Slug = slug ?? $"sc_{id}",
                    Parent = parent,
                    Type = isPublic ? ResourceType.StorageCollection : ResourceType.IIIFCollection
                }
            ]
        });
    }
}
