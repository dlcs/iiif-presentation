using Core.Exceptions;
using Models.API.General;
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
    public static async Task<T?> ReadAsJsonAsync<T>(this HttpResponseMessage response,
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

        try
        {
            return serializer.Deserialize<T>(jsonReader);
        }
        catch (Exception exception)
        {
            return serializer.Deserialize<T>(jsonReader);
        }
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
    
    public static async Task<T?> ReadAsPresentationResponseAsync<T>(this HttpResponseMessage response,
        JsonSerializerSettings? settings = null)
    {
        if ((int)response.StatusCode < 400)
        {
            return await response.ReadWithContext<T>(true, settings);
        }

        Error? error;
        try
        {
            error = await response.ReadAsJsonAsync<Error>(false, settings);
        }
        catch (Exception ex)
        {
            throw new PresentationException("Could not find a Hydra error in response", ex);
        }

        if (error != null)
        {
            throw new PresentationException(error.Description);
        }

        throw new PresentationException("Unable to process error condition");
    }

    private static async Task<T?> ReadWithContext<T>(
        this HttpResponseMessage response,
        bool ensureSuccess,
        JsonSerializerSettings? settings)
    {
        var json = await response.ReadAsJsonAsync<T>(ensureSuccess, settings);
        return json;
    }
}