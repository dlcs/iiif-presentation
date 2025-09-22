using System.Text.RegularExpressions;
using API.Helpers;
using Core.Exceptions;
using Core.Web;
using DLCS;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.Extensions.Options;
using Models.DLCS;

namespace API.Features.Manifest;

public partial class PaintableAssetIdentifier(IOptionsMonitor<DlcsSettings> dlcsOptionsMonitor, ILogger<PaintableAssetIdentifier> logger)
{
    private DlcsSettings DlcsSettings => dlcsOptionsMonitor.CurrentValue;
    
    /// <summary>
    /// Get <see cref="AssetId"/> from <see cref="IPaintable"/>, if it represents an asset managed by this instance, otherwise <c>null</c>
    /// </summary>
    /// <param name="paintable">Paintable resource, can be null</param>
    /// <param name="customerId">Id of the customer making the current request</param>
    /// <returns><see cref="AssetId"/>, if the provided <see cref="IPaintable"/> represents DLCS managed asset</returns>
    public AssetId? ResolvePaintableAsset(IPaintable? paintable, int customerId)
        => paintable switch
        {
            Image image => Resolve(image, customerId),
            Sound sound => Resolve(sound, customerId),
            Video video => Resolve(video, customerId),
            // If there are multiple transcodes available the body may be a PaintingChoice in which case each choice can be checked.
            PaintingChoice choice => choice.Items
                ?.Select(choicePaintable => ResolvePaintableAsset(choicePaintable, customerId)).OfType<AssetId>()
                .FirstOrDefault(),
            _ => null
        };

    private AssetId? Resolve(ExternalResource av, int customerId)
    {
        // AV are identified from body id only.
        return ParseId(CanonicalAvRegex(), av.Id, customerId);
    }
    
    private AssetId? Resolve(Image image, int customerId)
    {
        var fromServices = ResolveFromImageServices(image.Service, customerId);
        var fromBody = ParseId(CanonicalImageRegex(), image.Id, customerId);

        // Explicit ban on ambiguity
        if (fromServices is not null
            && fromBody is not null
            && fromServices != fromBody)
            throw new PresentationException("Image body and services point to different managed assets");

        return fromServices ?? fromBody;
    }

    private AssetId? ResolveFromImageServices(List<IService>? services, int customerId)
    {
        if (services is null) return null;

        foreach (var service in services)
        {
            switch (service)
            {
                case ImageService3 service3:
                    if (ParseId(CanonicalImageRegex(), service3.Id, customerId) is {} result3) return result3;
                    break;
                case ImageService2 service2:
                    if (ParseId(CanonicalImageRegex(), service2.Id, customerId) is {} result2) return result2;
                    break;
                // default: break;
            }
        }

        return null; // no recognizable asset id found
    }
    

    private AssetId? ParseId(Regex regex, string? id, int customerId)
    {
        if (id is null) return null;
        if (!Uri.TryCreate(id, UriKind.Absolute, out var uri)) return null;
        if (!IsHostKnown(uri))
            return null; // only assets from known domains (self or as-configured)

        var canonicalMatch = regex.Match(uri.AbsolutePath);
        if (!canonicalMatch.Success) return null;
        if (int.Parse(canonicalMatch.Groups["customer"].Value) != customerId)
        {
            logger.LogTrace("Asset path '{Path}' does not belong to customer '{CustomerId}'", uri.AbsolutePath, customerId);
            return null;
        }
        
        return new AssetId(customerId, int.Parse(canonicalMatch.Groups["space"].Value), canonicalMatch.Groups["asset"].Value);
    }

    /// <summary>
    /// Checks the "host" part of the URI against configured Orchestrator uri host
    /// </summary>
    /// <param name="uri">Asset URI</param>
    /// <returns><c>true</c>if the provided URI's host is recognized orcherstrator id</returns>
    private bool IsHostKnown(Uri uri) =>
        DlcsSettings.OrchestratorUri is { } orchestratorUri
        && uri.Host.Equals(orchestratorUri.Host, StringComparison.OrdinalIgnoreCase);
    
    // Canonical path: /iiif-img/{version?}/{customer}/{space}/{asset}/{image-request}
    [GeneratedRegex("""\/?iiif-img/((v2|v3)\/)?(?<customer>\d+)\/(?<space>\d+)/(?<asset>[^\/\s]+)\/?""")]
    private static partial Regex CanonicalImageRegex();
    
    // Audio: /iiif-av/{customer}/{space}/{asset}/full/max/default.{extension}
    // Video: /iiif-av/{customer}/{space}/{asset}/full/full/max/max/0/default.{extension}
    [GeneratedRegex("""\/?iiif-av/(?<customer>\d+)\/(?<space>\d+)/(?<asset>[^\/\s]+)\/?""")]
    private static partial Regex CanonicalAvRegex();
}
