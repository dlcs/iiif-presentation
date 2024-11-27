using System.Runtime.CompilerServices;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Models.Database;
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
        string parent = "root", LanguageMap? label = null)
    {
        createdDate ??= DateTime.UtcNow;
        return manifests.AddAsync(new Manifest
        {
            Id = id,
            CustomerId = customer,
            CreatedBy = "Admin",
            Created = createdDate.Value,
            Modified = createdDate.Value,
            Label = label,
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

    public static ValueTask<EntityEntry<CanvasPainting>> AddTestCanvasPainting(
        this DbSet<CanvasPainting> canvasPaintings, Manifest manifest, string? id = null, int? canvasOrder = null,
        int? choiceOrder = null, DateTime? createdDate = null, int? width = null, int? height = null,
        Uri? canvasOriginalId = null)
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
        };
        manifest.CanvasPaintings.Add(canvasPainting);
        return canvasPaintings.AddAsync(canvasPainting);
    }

    public static CanvasPainting WithCanvasPainting(this EntityEntry<Manifest> entry,
        string? id = null, int? canvasOrder = null, int? choiceOrder = null, DateTime? createdDate = null,
        int? width = null, int? height = null, Uri? canvasOriginalId = null)
    {
        createdDate ??= DateTime.UtcNow;
        var manifest = entry.Entity;
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
        };
        manifest.CanvasPaintings.Add(canvasPainting);
        return canvasPainting;
    }
}
