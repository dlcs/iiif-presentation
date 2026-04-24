using System.Collections.Concurrent;
using System.Net;
using DLCS.Exceptions;
using DLCS.Handlers;
using DLCS.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace DLCS.API;

public interface IDlcsApiClient
{
    /// <summary>
    /// Tests to see if the current HTTP request context is authenticated for specified customer 
    /// </summary>
    Task<bool> IsRequestAuthenticated(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new space with given name for customer
    /// </summary>
    Task<Space> CreateSpace(int customerId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingest deliverables into the DLCS
    /// </summary>
    public Task<List<Batch>> IngestDeliverables<T>(int customerId, List<T> deliverables, bool adjunctQueue = false,
        CancellationToken cancellationToken = default);

    Task<List<JObject>> GetBatchAssets(int customerId, int batchId,
        CancellationToken cancellationToken);

    Task<IList<JObject>> GetCustomerImages(int customerId, ICollection<string> assetIds,
        CancellationToken cancellationToken = default);

    Task<IList<JObject>> GetCustomerImages(int customerId, string manifestId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an asset with a new manifest
    /// </summary>
    /// <param name="customerId">the customer id</param>
    /// <param name="assets">assets to update</param>
    /// <param name="operationType">whether to add, remove or replace</param>
    /// <param name="manifests">manifests to update</param>
    public Task<Asset[]> UpdateAssetManifest(int customerId, ICollection<string> assets, OperationType operationType,
        List<string> manifests, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes adjuncts from assets
    /// </summary>
    /// <param name="customerId">The customer id</param>
    /// <param name="bulkDelete">The adjuncts to delete</param>
    public Task DeleteAdjuncts(int customerId, IEnumerable<AdjunctAssetIdentifier> bulkDelete,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="IDlcsApiClient"/>
/// </summary>
/// <remarks>Note that this required <see cref="AmbientAuthHandler"/> to work</remarks>
internal class DlcsApiClient(
    HttpClient httpClient,
    IOptions<DlcsSettings> dlcsOptions,
    ILogger<DlcsApiClient> logger) : IDlcsApiClient
{
    private readonly DlcsSettings settings = dlcsOptions.Value;
    
    public async Task<bool> IsRequestAuthenticated(int customerId, CancellationToken cancellationToken = default)
    {
        var customerPath = $"/customers/{customerId}";
        var response = await httpClient.GetAsync(customerPath, cancellationToken);

        // if DLCS returns 200 then credentials have access
        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<Space> CreateSpace(int customerId, string name, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Creating new space for customer {CustomerId}, {SpaceName}", customerId, name);
        var spacePath = $"/customers/{customerId}/spaces";
        var payload = new Space { Name = name };

        var space = await CallDlcsApiFor<Space>(HttpMethod.Post, spacePath, payload, cancellationToken);
        return space ?? throw new DlcsException("Failed to create space", HttpStatusCode.InternalServerError);
    }
    
    public async Task<List<Batch>> IngestDeliverables<T>(int customerId, List<T> deliverables, bool adjunctQueue = false, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Creating new batch for customer {CustomerId} with {NumberOfDeliverables} {Type}", customerId,
            deliverables.Count, adjunctQueue ? "adjuncts" : "assets");
        var queuePath = $"/customers/{customerId}/{(adjunctQueue ? "adjunctQueue" : "queue")}";
        
        var chunkedDeliverableList = deliverables.Chunk(settings.MaxBatchSize);
        var batches = new ConcurrentBag<Batch>();

        var tasks = chunkedDeliverableList.Select(async chunkedAssets =>
        {
            var hydraImages = new HydraCollection<T>(chunkedAssets);

            var batch = await CallDlcsApiFor<Batch>(HttpMethod.Post, queuePath, hydraImages, cancellationToken);
            if (batch == null)
            {
                logger.LogError("Could not understand the batch response for customer {CustomerId}", customerId);
                throw new DlcsException("Failed to create batch", HttpStatusCode.InternalServerError);
            }
            
            batches.Add(batch);
        });
        await Task.WhenAll(tasks);
        
        return batches.ToList();
    }

    public async Task<List<JObject>> GetBatchAssets(int customerId, int batchId,
        CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Requesting assets for batch {BatchId} for customer {CustomerId}", batchId, customerId);
        var endpoint = $"/customers/{customerId}/queue/batches/{batchId}/assets";

        var assets = await CallDlcsApiForJson(HttpMethod.Get, endpoint, null, cancellationToken);

        return assets switch
        {
            null => [],
            not null when assets.TryGetValue("member", out var member)
                          && member.Type == JTokenType.Array => member.OfType<JObject>().ToList(),
            _ => []
        };
    }

    public async Task<IList<JObject>> GetCustomerImages(int customerId, ICollection<string> assetIds,
        CancellationToken cancellationToken = default)
    {
        if (assetIds.Count == 0)
            return [];
        
        logger.LogTrace("Requesting images for customer {CustomerId}: {AssetIds}", customerId,
            string.Join(",", assetIds));

        var endpoint = $"/customers/{customerId}/allImages?include=adjuncts";
        var results = new List<JObject>();
        foreach (var idBatch in assetIds.Distinct().Chunk(settings.MaxImageListSize))
        {
            // duplicate images cause errors in the DLCS, so strip them out
            var hydraImages = new HydraCollection<JObject>(idBatch.Select(id => new JObject {["id"] = id}).ToArray());

            var result =
                await CallDlcsApiFor<HydraCollection<JObject>>(HttpMethod.Post, endpoint, hydraImages,
                    cancellationToken);

            if (result?.Members is {Length: > 0} batchResults)
                results.AddRange(batchResults);
        }

        return results;
    }
    
    public async Task<IList<JObject>> GetCustomerImages(int customerId, string manifestId,
        CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Requesting images for customer {CustomerId} for the manifest {ManifestId}", customerId,
            manifestId);
        
        var results = new List<JObject>();
        var endpoint = $"/customers/{customerId}/allImages?q={{\"manifests\": [\"{manifestId}\"]}}&pageSize={settings.MaxImageListSize}&page=1&include=adjuncts";

        while (endpoint != null)
        {
            var result =
                await CallDlcsApiFor<HydraCollection<JObject>>(HttpMethod.Get, endpoint, null, cancellationToken);
            
            if (result?.Members is {Length: > 0} pageResults)
                results.AddRange(pageResults);
            
            endpoint = result?.View?.Next != null ? new Uri(result.View.Next).PathAndQuery : null;
        }
        
        return results;
    }
    
    public async Task<Asset[]> UpdateAssetManifest(int customerId, ICollection<string> assets, OperationType operationType, List<string> manifests,
        CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Updating assets for customer {CustomerId} to {OperationType} manifests",
            customerId, operationType.ToString());

        var distinctAssets = assets.Distinct().ToList();
        
        var chunkedAssetList = distinctAssets.Chunk(settings.MaxBatchSize);
        var assetsResponse = new ConcurrentBag<Asset>();
        var endpoint = $"/customers/{customerId}/allImages";

        var tasks = chunkedAssetList.Select(async chunkedAssets =>
        {
            var allImages = new BulkPatchAssets
            {
                Field = "manifests",
                Members = chunkedAssets.Select(a => new IdentifierOnly(a)).ToList(),
                Operation = operationType,
                Value = manifests
            };

            var response = await CallDlcsApiFor<HydraCollection<Asset>>(HttpMethod.Patch, endpoint, allImages, cancellationToken);
            if (response == null)
            {
                logger.LogError("Could not understand the patch all assets response for customer {CustomerId}", customerId);
                throw new DlcsException("Failed to create batch", HttpStatusCode.InternalServerError);
            }
            
            foreach (var responseMember in response.Members)
            {
                assetsResponse.Add(responseMember);
            }
        });
        
        await Task.WhenAll(tasks);
        
        // this is extremely unlikely to happen, as the DLCS should have already been checked at this point
        if (assetsResponse.Count != distinctAssets.Count)
        {
            var missingAssets = assets.Where(a => assetsResponse.All(ar => a != $"{customerId}/{ar.Space}/{ar.Id}")).ToList();

            logger.LogError(
                "Received less assets than expected when patching customer images for {CustomerId}, assets missing - {MissingAssets}",
                customerId, missingAssets);
            throw new DlcsException($"Could not find assets [{string.Join(',', missingAssets)}] in DLCS", HttpStatusCode.InternalServerError);
        }
        
        return assetsResponse.ToArray();
    }
    
    public async Task DeleteAdjuncts(int customerId, IEnumerable<AdjunctAssetIdentifier> bulkDelete,
        CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Deleting adjuncts for customer {CustomerId}", customerId);

        var chunks = ChunkByAdjunctCount(bulkDelete, settings.MaxImageListSize);

        foreach (var chunk in chunks)
        {
            var payload = new HydraCollection<AdjunctAssetIdentifier>(chunk);
            var endpoint = $"/customers/{customerId}/deleteAdjuncts";

            await CallDlcsApiForJson(HttpMethod.Delete, endpoint, payload, cancellationToken);
        }
    }

    private static IEnumerable<AdjunctAssetIdentifier[]> ChunkByAdjunctCount(
        IEnumerable<AdjunctAssetIdentifier> source, int maxAdjunctCount)
    {
        var currentChunk = new List<AdjunctAssetIdentifier>();
        var currentCount = 0;

        foreach (var item in source)
        {
            // A single asset may have more adjuncts than the per-request limit. Pre-split the adjunct
            // list so each resulting sub-item is within the limit before the accumulation logic runs.
            foreach (var adjunctBatch in item.Adjunct.Chunk(maxAdjunctCount))
            {
                var splitItem = new AdjunctAssetIdentifier { Id = item.Id, Adjunct = [..adjunctBatch] };

                if (currentCount + splitItem.Adjunct.Count > maxAdjunctCount && currentChunk.Count > 0)
                {
                    yield return currentChunk.ToArray();
                    currentChunk.Clear();
                    currentCount = 0;
                }

                currentChunk.Add(splitItem);
                currentCount += splitItem.Adjunct.Count;
            }
        }

        if (currentChunk.Count > 0) yield return currentChunk.ToArray();
    }

    private async Task<JObject?> CallDlcsApiForJson(HttpMethod httpMethod, string path, object? payload,
        CancellationToken cancellationToken)
    {
        var response = await CallDlcsApi(httpMethod, path, payload, cancellationToken);
        return await response.ReadAsJsonResponse(cancellationToken);
    }

    private async Task<T?> CallDlcsApiFor<T>(HttpMethod httpMethod, string path, object? payload,
        CancellationToken cancellationToken)
    {
        var response = await CallDlcsApi(httpMethod, path, payload, cancellationToken);
        return await response.ReadAsDlcsResponse<T>(cancellationToken);
    }

    private async Task<HttpResponseMessage> CallDlcsApi(HttpMethod httpMethod, string path, object? payload,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(httpMethod, path);
        if (payload != null)
            request.Content = DlcsHttpContent.GenerateJsonContent(payload);

        var response = await httpClient.SendAsync(request, cancellationToken);
        return response;
    }
}
