using Core.Exceptions;
using IIIF;
using IIIF.Serialisation;
using Newtonsoft.Json;

namespace Core.Response;

public static class HttpResponseMessageX
{
    /// <summary>
    /// Read HttpResponseMessage as JSON using Newtonsoft for conversion.
    /// </summary>
    /// <param name="response"><see cref="HttpResponseMessage"/> object</param>
    /// <param name="ensureSuccess">If true, will validate that the response is a 2xx.</param>
    /// <param name="settings"></param>
    /// <typeparam name="T">Type to convert response to</typeparam>
    /// <returns>Converted Http response.</returns>
    public static async Task<T?> ReadAsIIIFJsonAsync<T>(this HttpResponseMessage response,
        bool ensureSuccess = true, JsonSerializerSettings? settings = null) where T : JsonLdBase
    {
        if (ensureSuccess) response.EnsureSuccessStatusCode();

        if (!response.IsJsonResponse()) return default;

        var contentStream = await response.Content.ReadAsStreamAsync();
        
        return contentStream.FromJsonStream<T>();
    }
    
    /// <summary>
    /// Read HttpResponseMessage as JSON using Newtonsoft for conversion.
    /// </summary>
    /// <param name="response"><see cref="HttpResponseMessage"/> object</param>
    /// <param name="ensureSuccess">If true, will validate that the response is a 2xx.</param>
    /// <param name="settings"></param>
    /// <typeparam name="T">Type to convert response to</typeparam>
    /// <returns>Converted Http response.</returns>
    public static async Task<T?> ReadAsPresentationJsonAsync<T>(this HttpResponseMessage response,
        bool ensureSuccess = true, JsonSerializerSettings? settings = null)
    {
        if (ensureSuccess) response.EnsureSuccessStatusCode();

        if (!response.IsJsonResponse()) return default;

        var contentStream = await response.Content.ReadAsStreamAsync();
        
        using var streamReader = new StreamReader(contentStream);
        using var jsonReader = new JsonTextReader(streamReader);

        JsonSerializer serializer = new();
        if (settings == null) return serializer.Deserialize<T>(jsonReader);
        
        if (settings.ContractResolver != null)
        {
            serializer.ContractResolver = settings.ContractResolver;
        }
        serializer.NullValueHandling = settings.NullValueHandling;
        
        return serializer.Deserialize<T>(jsonReader);
    }
    
    /// <summary>
    /// Check if the <see cref="HttpResponseMessage"/> object contains a JSON response
    /// e.g. application/json, application/ld+json
    /// </summary>
    /// <param name="response"><see cref="HttpResponseMessage"/> object</param>
    /// <returns></returns>
    public static bool IsJsonResponse(this HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return mediaType != null && mediaType.Contains("json");
    }
    
    public static async Task<T?> ReadAsIIIFResponseAsync<T>(this HttpResponseMessage response,
        JsonSerializerSettings? settings = null) where T : JsonLdBase
    {
        if ((int)response.StatusCode < 400)
        {
            return await response.ReadWithIIIFContext<T>(true, settings);
        }
        
        try
        {
            return await response.ReadAsIIIFJsonAsync<T>(false, settings);
        }
        catch (Exception ex)
        {
            throw new PresentationException("Could not convert response JSON to error", ex);
        }
    }
    
    public static async Task<T?> ReadAsPresentationResponseAsync<T>(this HttpResponseMessage response,
        JsonSerializerSettings? settings = null)
    {
        if ((int)response.StatusCode < 400)
        {
            return await response.ReadWithContext<T>(true, settings);
        }
        
        try
        {
            return await response.ReadAsPresentationJsonAsync<T>(false, settings);
        }
        catch (Exception ex)
        {
            throw new PresentationException("Could not convert response JSON to error", ex);
        }
    }

    private static async Task<T?> ReadWithIIIFContext<T>(
        this HttpResponseMessage response,
        bool ensureSuccess,
        JsonSerializerSettings? settings) where T : JsonLdBase
    {
        var json = await response.ReadAsIIIFJsonAsync<T>(ensureSuccess, settings ?? new JsonSerializerSettings());
        return json;
    }
    
    private static async Task<T?> ReadWithContext<T>(
        this HttpResponseMessage response,
        bool ensureSuccess,
        JsonSerializerSettings? settings)
    {
        var json = await response.ReadAsPresentationJsonAsync<T>(ensureSuccess, settings ?? new JsonSerializerSettings());
        return json;
    }
}