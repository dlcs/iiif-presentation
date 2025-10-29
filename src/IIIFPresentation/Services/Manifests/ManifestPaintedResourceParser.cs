using System.Collections.Immutable;
using System.Text;
using Core.Exceptions;
using Core.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Paths;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Services.Manifests;

/// <summary>
/// Contains logic for parsing a Manifests "paintedResources" property into <see cref="CanvasPainting"/> entities
/// </summary>
public class ManifestPaintedResourceParser(
    IPathRewriteParser pathRewriteParser,
    IPresentationPathGenerator presentationPathGenerator,
    PresentationContext dbContext,
    ILogger<ManifestPaintedResourceParser> logger)
{
    public async Task<IEnumerable<InterimCanvasPainting>> ParseToCanvasPainting(PresentationManifest presentationManifest,
        int customerId, string? existingManifestId = null)
    {
        if (presentationManifest.PaintedResources.IsNullOrEmpty()) return [];

        var paintedResources = presentationManifest.PaintedResources;
        var canvasPaintings = new List<InterimCanvasPainting>();

        using var logScope = logger.BeginScope("Manifest {ManifestId}", presentationManifest.Id);

        var existingCanvasIds = await LoadCustomerExistingCanvasIds(customerId,existingManifestId);
        
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

            var cp = CreatePartialCanvasPainting(customerId, paintedResource, canvasOrder, implicitOrdering, existingCanvasIds);

            count++;
            canvasPaintings.Add(cp);
        }

        ThrowIfAnyCanvasPaintingsFailed(canvasPaintings);

        return canvasPaintings;
    }

    private static void ThrowIfAnyCanvasPaintingsFailed(ICollection<InterimCanvasPainting> canvasPaintings)
    {
        var failedPaintings = canvasPaintings.OfType<FailedInterimCanvasPainting>().ToList();
        if (failedPaintings.Count == 0) return;
        
        throw new CanvasPaintingValidationException(failedPaintings.Select(p=>(p.Id,p.Reason)));
    }

    private async Task<ISet<string>> LoadCustomerExistingCanvasIds(int customerId, string? exceptInManifest)
    {
        var customerPaintingsQuery =
            dbContext.CanvasPaintings.AsNoTracking().Where(painting => painting.CustomerId == customerId);

        if (exceptInManifest is { Length: > 0 })
        {
            customerPaintingsQuery =
                customerPaintingsQuery.Where(painting => painting.ManifestId != exceptInManifest);
        }

        var results = await customerPaintingsQuery.Select(painting => painting.Id).Distinct().ToListAsync();
        return results.ToImmutableHashSet();
    }

    private InterimCanvasPainting CreatePartialCanvasPainting(int customerId, PaintedResource paintedResource,
        int canvasOrder, bool implicitOrdering, ISet<string> existingCanvasIds)
    {
        var specifiedCanvasId = TryGetValidCanvasId(customerId, paintedResource);
        // Check if not used in a different manifest of this user - we will check for failures after we check all resources
        if (specifiedCanvasId != null
            && existingCanvasIds.Contains(specifiedCanvasId))
        {
            return new FailedInterimCanvasPainting
            {
                Id = specifiedCanvasId,
                Reason = "Id already used in a different manifest"
            };
        }

        var payloadCanvasPainting = paintedResource.CanvasPainting;
        var (space, assetId) =
            GetCanvasPaintingDetailsForAsset(paintedResource.Asset.ThrowIfNull(nameof(paintedResource.Asset)));
        logger.LogTrace("Processing canvas painting for asset {AssetId}", assetId);
        var cp = new InterimCanvasPainting
        {
            Id = specifiedCanvasId!, // might be null, but is `null!` in prop initializer
            Label = payloadCanvasPainting?.Label,
            CanvasLabel = payloadCanvasPainting?.CanvasLabel,
            CanvasOrder = canvasOrder,
            SuspectedAssetId = assetId,
            SuspectedSpace = space,
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

        var parsedCanvasId = pathRewriteParser.ParsePathWithRewrites(canvasId.Host, canvasId.AbsolutePath, customerId);
        CanvasHelper.CheckParsedCanvasIdForErrors(parsedCanvasId, canvasId.AbsolutePath, logger);

        return parsedCanvasId.Resource;
    }

    private static (int? space, string id) GetCanvasPaintingDetailsForAsset(JObject asset)
    {
        // Read props from Asset - id must be there. If not, throw an exception
        var space = asset.TryGetValue<int?>(AssetProperties.Space);
        var id = asset.GetRequiredValue<string>(AssetProperties.Id);
        return (space, id);
    }
}
