using Core.Exceptions;
using Core.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Paths;
using Services.Manifests.Exceptions;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using Services.Manifests.Settings;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Services.Manifests;

/// <summary>
/// Contains logic for parsing a Manifests "paintedResources" property into <see cref="CanvasPainting"/> entities
/// </summary>
public class ManifestPaintedResourceParser(
    IPathRewriteParser pathRewriteParser,
    IPresentationPathGenerator presentationPathGenerator,
    IOptions<PathSettings> options,
    PresentationContext dbContext,
    ILogger<ManifestPaintedResourceParser> logger)
{
    private readonly PathSettings settings = options.Value;
    
    public async Task<IEnumerable<InterimCanvasPainting>> ParseToCanvasPainting(PresentationManifest presentationManifest,
        int customerId, string? existingManifestId = null)
    {
        if (presentationManifest.PaintedResources.IsNullOrEmpty()) return [];

        var paintedResources = presentationManifest.PaintedResources;
        var canvasPaintings = new List<InterimCanvasPainting>();

        using var logScope = logger.BeginScope("Manifest {ManifestId}", presentationManifest.Id);
        
        var count = 0;
        foreach (var paintedResource in paintedResources)
        {
            if (paintedResource.Asset == null)
            {
                logger.LogInformation("Manifest {ManifestId}:{Customer}, index {Index} ignored as no asset",
                    presentationManifest.Id, customerId, count);
                continue;
            }

            var canvasOrder = paintedResource.CanvasPainting?.CanvasOrder ?? count;
            var implicitOrdering = paintedResource.CanvasPainting?.CanvasOrder == null;

            var cp = CreatePartialCanvasPainting(customerId, paintedResource, canvasOrder, implicitOrdering);

            count++;
            canvasPaintings.Add(cp);
        }

        await CheckInterimCanvasIds(canvasPaintings, customerId, existingManifestId);

        return canvasPaintings;
    }

    private async Task CheckInterimCanvasIds(ICollection<InterimCanvasPainting> canvasPaintings, int customerId,
        string? exceptInManifest)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract - contract lies
        var canvasPaintingIds = canvasPaintings
            .Select(cp => cp.Id)
            .Where(id => id != null)
            .Distinct()
            .ToList();

        var customerPaintingsQuery =
            dbContext.CanvasPaintings.AsNoTracking()
                .Where(painting => painting.CustomerId == customerId)
                .Where(painting => canvasPaintingIds.Contains(painting.Id));

        if (exceptInManifest is { Length: > 0 })
        {
            customerPaintingsQuery =
                customerPaintingsQuery.Where(painting => painting.ManifestId != exceptInManifest);
        }

        var results = await customerPaintingsQuery.Select(painting => painting.Id).Distinct().ToListAsync();

        // `results` now contains any canvas ids from manifests of this customer, that also were found in created canvas paintings
        // for a successful operation the results should be empty
        if (results.Count == 0) return;

        throw new CanvasPaintingValidationException(results.Select(p => (p, "Id used in one of your other manifests")));
    }

    private InterimCanvasPainting CreatePartialCanvasPainting(int customerId, PaintedResource paintedResource,
        int canvasOrder, bool implicitOrdering)
    {
        var specifiedCanvasId = TryGetValidCanvasId(customerId, paintedResource);
        var payloadCanvasPainting = paintedResource.CanvasPainting;
        var assetDetails =
            GetCanvasPaintingDetailsForAsset(paintedResource.Asset.ThrowIfNull(nameof(paintedResource.Asset)), customerId);

        if (assetDetails.Space < 0)
        {
            throw new AssetException(
                $"The space for asset '{assetDetails.Id}' {(specifiedCanvasId != null ? $"with canvas id '{specifiedCanvasId}' " : "")}is '{assetDetails.Space}' and cannot be negative",
                assetDetails.Id);
        }

        logger.LogTrace("Processing canvas painting for asset {AssetId}", assetDetails.Id);
        var cp = new InterimCanvasPainting
        {
            Id = specifiedCanvasId!, // might be null, but is `null!` in prop initializer
            Label = payloadCanvasPainting?.Label,
            CanvasLabel = payloadCanvasPainting?.CanvasLabel,
            CanvasOrder = canvasOrder,
            SuspectedAssetId = assetDetails.Id,
            SuspectedSpace = assetDetails.Space,
            SuspectedAdjuncts = assetDetails.Adjuncts,
            ChoiceOrder = payloadCanvasPainting?.ChoiceOrder,
            Ingesting = payloadCanvasPainting?.Ingesting ?? false,
            StaticWidth = payloadCanvasPainting?.StaticWidth,
            StaticHeight = payloadCanvasPainting?.StaticHeight,
            Duration = payloadCanvasPainting?.Duration,
            Target = payloadCanvasPainting?.Target,
            CustomerId = customerId,
            CanvasPaintingType = CanvasPaintingType.PaintedResource,
            CanvasOriginalId = payloadCanvasPainting?.CanvasOriginalId != null
                ? CanvasOriginalHelper.TryGetValidCanvasOriginalId(presentationPathGenerator, customerId,
                    payloadCanvasPainting.CanvasOriginalId)
                : null,
            Thumbnail = payloadCanvasPainting?.Thumbnail == null
                ? null
                : Uri.TryCreate(payloadCanvasPainting.Thumbnail, UriKind.Absolute, out var thumbnail)
                    ? thumbnail
                    : null,
            ImplicitOrder = implicitOrdering
        };
        
        return cp;
    }

    private string? TryGetValidCanvasId(int customerId, PaintedResource paintedResource)
    {
        paintedResource.CanvasPainting ??= new();

        var canvasPainting = paintedResource.CanvasPainting;

        if (canvasPainting.CanvasId == null) return null;

        if (!Uri.TryCreate(canvasPainting.CanvasId, UriKind.Absolute, out var canvasId))
        {
            CanvasHelper.CheckForProhibitedCharacters(canvasPainting.CanvasId, logger);
            return canvasPainting.CanvasId;
        }

        if (!settings.IsCustomerRecognisedHost(customerId, canvasId.Host)) 
        {
            throw new InvalidCanvasIdException(canvasPainting.CanvasId,
                $"The host for canvas id '{canvasPainting.CanvasId}' could not be recognised");
        }
        
        var parsedCanvasId = pathRewriteParser.ParsePathWithRewrites(canvasId.Host, canvasId.AbsolutePath, customerId);
        CanvasHelper.CheckParsedCanvasIdForErrors(parsedCanvasId, canvasId.AbsolutePath, logger);
        
        if (customerId != parsedCanvasId.Customer)
        {
            throw new InvalidCanvasIdException(canvasPainting.CanvasId,
                $"The customer parsed from the canvas id does not match the customer found from the calling URL");
        }

        return parsedCanvasId.Resource;
    }

    private static AssetDetails GetCanvasPaintingDetailsForAsset(JObject asset, int customerId)
    {
        // Read props from Asset - id must be there. If not, throw an exception
        var adjuncts = asset.TryGetCollectionValue<Adjunct>(AssetProperties.Adjuncts);
        asset.Remove(AssetProperties.Adjuncts);
        var id = asset.GetRequiredValue<string>(AssetProperties.Id);
        var space = asset.TryGetValue<int?>(AssetProperties.Space);
        return new AssetDetails
        {
            Space = space,
            Adjuncts = adjuncts != null ? HydrateAdjuncts(adjuncts, customerId, id, space) : null,
            Id = id
        };
    }

    private static List<Adjunct> HydrateAdjuncts(IEnumerable<Adjunct> adjuncts, int customerId, string assetId, int? space)
    {
        var resolvedSpace = space ?? SpaceHelper.DefaultSpaceForLaterPopulation;
        return adjuncts.Select(a =>
        {
            a.AssetId ??= new AssetId(customerId, resolvedSpace, assetId);
            return a;
        }).ToList();
    }
    
    /// <summary>
    /// Parsed details extracted from an asset <see cref="JObject"/> within a <see cref="PaintedResource"/>
    /// </summary>
    private class AssetDetails
    {
        public int? Space { get; init; }
        public List<Adjunct>? Adjuncts { get; init; }
        public required string Id { get; init; }
    }
}
