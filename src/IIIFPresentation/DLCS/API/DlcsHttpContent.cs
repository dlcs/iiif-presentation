using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Helpers;
using DLCS.Converters;
using DLCS.Exceptions;
using DLCS.Models;
using IIIF.Serialisation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonLdBase = IIIF.JsonLdBase;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace DLCS.API;

internal static class DlcsHttpContent
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JTokenConverter() }
    };
        
    /// <summary>
    /// Generate <see cref="StringContent"/> containing object serialised as JSON
    /// </summary>
    /// <param name="body">Body to serialise</param>
    /// <typeparam name="T">Type of object being serialised</typeparam>
    /// <returns><see cref="StringContent"/> object</returns>
    public static StringContent GenerateJsonContent<T>(T body)
    {
        var jsonString = JsonSerializer.Serialize(body.ThrowIfNull(nameof(body)), JsonSerializerOptions);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        return content;
    }
    
    public static async Task<T?> ReadAsDlcsResponse<T>(this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.ReadDlcsModel<T>(true, cancellationToken);
        }

        throw await CheckAndThrowResponseError(response, cancellationToken);
    }

    public static async Task<JObject?> ReadAsJsonResponse(this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
            throw await CheckAndThrowResponseError(response, cancellationToken);

        try
        {
            return JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (JsonReaderException jre)
        {
            throw new DlcsException("Error reading DLCS response", jre, HttpStatusCode.InternalServerError);
        }
    }
    
    public static async Task<T?> ReadAsIIIFResponse<T>(this HttpResponseMessage response,
        CancellationToken cancellationToken = default) where T : JsonLdBase
    {
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadAsStreamAsync(cancellationToken)).FromJsonStream<T>();
        }

        throw await CheckAndThrowResponseError(response, cancellationToken);
    }

    private static async Task<DlcsException> CheckAndThrowResponseError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<DlcsError>(JsonSerializerOptions, cancellationToken);

            if (error != null)
            {
                return new DlcsException(error.Description, response.StatusCode);
            }

            throw new DlcsException("Unable to process error condition", response.StatusCode);
        }
        catch (Exception ex) when (ex is not DlcsException)
        {
            return new DlcsException("Could not find a DlcsError in response", ex, response.StatusCode);
        }
    }

    private static async Task<T?> ReadDlcsModel<T>(
        this HttpResponseMessage response,
        bool ensureSuccess,
        CancellationToken cancellationToken)
    {
        if (ensureSuccess) response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<T>(JsonSerializerOptions, cancellationToken);
        return json;
    }
}
