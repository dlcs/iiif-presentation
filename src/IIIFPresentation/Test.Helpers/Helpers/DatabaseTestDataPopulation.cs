using System.Runtime.CompilerServices;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Models.Database;
using Models.Database.Collections;
using Models.Database.General;
using Models.DLCS;

namespace Test.Helpers.Helpers;

/// <summary>
/// Helpers for creating resources for test
/// </summary>
public static class DatabaseTestDataPopulation
{
    public static ValueTask<EntityEntry<Manifest>> AddTestManifest(this DbSet<Manifest> manifests,
        [CallerMemberName] string id = "", int customer = 1, DateTime? createdDate = null, string? slug = null,
        string parent = "root", LanguageMap? label = null, int? batchId = null, bool ingested = false,
        int? spaceId = null, List<CanvasPainting>? canvasPaintings = null)
    {
        createdDate ??= DateTime.UtcNow;
        return manifests.AddAsync(new Manifest
        {
            Id = id,
            SpaceId = spaceId,
            CustomerId = customer,
            CreatedBy = "Admin",
            Created = createdDate.Value,
            Modified = createdDate.Value,
            Label = label,
            LastProcessed = ingested ? DateTime.UtcNow : null,
            Etag = Guid.NewGuid(),
            Hierarchy =
            [
                new Hierarchy
                {
                    Canonical = true,
                    Slug = slug ?? $"sm_{id}",
                    Parent = parent,
                    Type = ResourceType.IIIFManifest
                }
            ],
            CanvasPaintings = canvasPaintings,
            Batches = batchId != null ?
            [
                new Batch
                {
                    Id = batchId.Value,
                    Submitted = DateTime.UtcNow,
                    Status = ingested ? BatchStatus.Completed : BatchStatus.Ingesting,
                    ManifestId = id
                }
            ] : null,
        });
    }

    public static ValueTask<EntityEntry<Batch>> AddTestBatch(this DbSet<Batch> batches, int id, Manifest manifest)
    {
        manifest.Batches ??= [];
        var batch = new Batch
        {
            Id = id,
            CustomerId = manifest.CustomerId,
            ManifestId = manifest.Id,
            Status = BatchStatus.Ingesting,
        };
        manifest.Batches.Add(batch);
        return batches.AddAsync(batch);
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
    
    public static ValueTask<EntityEntry<Collection>> AddTestRootCollection(this DbSet<Collection> collections, int customer,
         DateTime? createdDate = null, bool isPublic = true)
    {
        createdDate ??= DateTime.UtcNow;
        return collections.AddAsync(new Collection
        {
            Id = "root",
            CustomerId = customer,
            CreatedBy = "Admin",
            Created = createdDate.Value,
            Modified = createdDate.Value,
            IsStorageCollection = true,
            IsPublic = isPublic,
            Hierarchy =
            [
                new Hierarchy
                {
                    Canonical = true,
                    Slug = "",
                    Type = isPublic ? ResourceType.StorageCollection : ResourceType.IIIFCollection
                }
            ]
        });
    }

    public static ValueTask<EntityEntry<CanvasPainting>> AddTestCanvasPainting(
        this DbSet<CanvasPainting> canvasPaintings, Manifest manifest, string? id = null, int? canvasOrder = null,
        int? choiceOrder = null, DateTime? createdDate = null, int? width = null, int? height = null,
        Uri? canvasOriginalId = null, LanguageMap? label = null, AssetId? assetId = null, bool ingesting = false)
    {
        createdDate ??= DateTime.UtcNow;
        manifest.CanvasPaintings ??= [];

        var canvasPaintingsCount = manifest.CanvasPaintings.Count;
        var canvasPainting = new CanvasPainting
        {
            Id = string.IsNullOrEmpty(id) ? $"{manifest}_{canvasPaintingsCount + 1}" : id,
            CanvasOrder = canvasOrder ?? canvasPaintingsCount,
            ChoiceOrder = choiceOrder,
            Created = createdDate.Value,
            Modified = createdDate.Value,
            StaticHeight = height,
            StaticWidth = width,
            CanvasOriginalId = canvasOriginalId,
            CustomerId = manifest.CustomerId,
            ManifestId = manifest.Id,
            Label = label,
            AssetId = assetId,
            Ingesting = ingesting,
        };
        manifest.CanvasPaintings.Add(canvasPainting);
        return canvasPaintings.AddAsync(canvasPainting);
    }
}
