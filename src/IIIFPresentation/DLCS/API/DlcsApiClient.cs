using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using DLCS.Exceptions;
using DLCS.Handlers;
using DLCS.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;

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
    /// Ingest assets into the DLCS
    /// </summary>
    public Task<List<Batch>> IngestAssets<T>(int customerId, List<T> images,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a list of assets from the DLCS
    /// </summary>
    public Task<HydraCollection<Asset>> RetrieveAllImages(int customerId, List<string> assets,
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
    DlcsSettings settings = dlcsOptions.Value;
    
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
        
        var space = await CallDlcsApi<Space>(HttpMethod.Post, spacePath, payload, cancellationToken);
        return space ?? throw new DlcsException("Failed to create space");
    }
    
    public async Task<List<Batch>> IngestAssets<T>(int customerId, List<T> assets, CancellationToken cancellationToken = default)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtCreate(customerId));
        
        logger.LogTrace("Creating new batch for customer {CustomerId} with {NumberOfAssets} assets", customerId,
            assets.Count);
        var queuePath = $"/customers/{customerId}/queue";
        
        var chunkedImageList = assets.Chunk(settings.MaxBatchSize);
        var batches = new ConcurrentBag<Batch>();

        var tasks = chunkedImageList.Select(async chunkedImages =>
        {
            var hydraImages = new HydraCollection<T>(chunkedImages);
            
            var batch = await CallDlcsApi<Batch>(HttpMethod.Post, queuePath, hydraImages, cancellationToken);
            if (batch == null)
            {
                logger.LogError("Could not understand the batch response for customer {CustomerId}", customerId);
                throw new DlcsException("Failed to create batch");
            }
            
            batches.Add(batch);
        });
        await Task.WhenAll(tasks);
        
        return batches.ToList();
    }

    public async Task<HydraCollection<Asset>> RetrieveAllImages(int customerId, List<string> assets,
        CancellationToken cancellationToken = default)
    {
        logger.LogTrace("performing an all images call for customer {CustomerId}", customerId);
        var allImagesPath = $"/customers/{customerId}/allImages";
        
        var allImagesRequest = new AllImages(assets);

        var asset =
            await CallDlcsApi<HydraCollection<Asset>>(HttpMethod.Post, allImagesPath, allImagesRequest, cancellationToken);
        
        return asset ?? throw new DlcsException("Failed to retrieve all images");
    }

    private async Task<T?> CallDlcsApi<T>(HttpMethod httpMethod, string path, object payload,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(httpMethod, path);
        request.Content = DlcsHttpContent.GenerateJsonContent(payload);

        var response = await httpClient.SendAsync(request, cancellationToken);
        return await response.ReadAsDlcsResponse<T>(cancellationToken);
    }
    
    private string JwtCreate(int customer)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "urn:presentation:default",
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, $"urn:dlcs:user:{customer}")]),
            Expires = DateTime.UtcNow.AddMinutes(120),
            
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(settings.ApiJwtKey)), SecurityAlgorithms.HmacSha256Signature),
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
        };

        var handler = new JsonWebTokenHandler	{ SetDefaultTimesOnTokenCreation = false };
	
        return handler.CreateToken(descriptor);
    }
}
