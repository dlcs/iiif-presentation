﻿using System.Runtime.Serialization;
using Core.Exceptions;
using Core.IIIF;
using IIIF;
using IIIF.Serialisation;
using Newtonsoft.Json;

namespace Core.Response;

public static class HttpResponseMessageX
{
    public static StreamingContext? SerializationContext { get; set; }

    /// <summary>
    /// Read HttpResponseMessage as JSON using Newtonsoft for conversion.
    /// </summary>
    /// <param name="response"><see cref="HttpResponseMessage"/> object</param>
    /// <param name="ensureSuccess">If true, will validate that the response is a 2xx.</param>
    /// <param name="settings"></param>
    /// <typeparam name="T">Type to convert response to</typeparam>
    /// <returns>Converted Http response.</returns>
    public static async Task<T?> ReadAsPresentationJsonAsync<T>(this HttpResponseMessage response,
        bool ensureSuccess = true, JsonSerializerSettings? settings = null) where T : JsonLdBase, new()
    {
        if (ensureSuccess) response.EnsureSuccessStatusCode();

        if (!response.IsJsonResponse()) return null;

        settings ??= new(IIIFSerialiserX.DeserializerSettings);
        if (SerializationContext.HasValue)
            settings.Context = SerializationContext.Value;

        var contentStream = await response.Content.ReadAsStreamAsync();
        var result = await contentStream.ToPresentation<T>(settings);
        return result;
    }

    /// <summary>
    /// Check if the <see cref="HttpResponseMessage"/> object contains a JSON response
    /// e.g. application/json, application/ld+json
    /// </summary>
    /// <param name="response"><see cref="HttpResponseMessage"/> object</param>
    /// <returns></returns>
    private static bool IsJsonResponse(this HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return mediaType != null && mediaType.Contains("json");
    }

    public static async Task<T?> ReadAsPresentationResponseAsync<T>(this HttpResponseMessage response,
        JsonSerializerSettings? settings = null) where T : JsonLdBase, new()
    {
        if ((int) response.StatusCode < 400)
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

    private static async Task<T?> ReadWithContext<T>(
        this HttpResponseMessage response,
        bool ensureSuccess,
        JsonSerializerSettings? settings) where T : JsonLdBase, new()
    {
        var json = await response.ReadAsPresentationJsonAsync<T>(ensureSuccess,
            settings ?? new JsonSerializerSettings(IIIFSerialiserX.DeserializerSettings));
        return json;
    }
}
