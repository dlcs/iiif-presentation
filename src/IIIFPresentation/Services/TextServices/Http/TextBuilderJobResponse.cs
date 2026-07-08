namespace Services.TextServices.Http;

/// <summary>
/// The fields needed from text-services' <c>JobResponse</c> - the body returned for
/// <c>POST</c>/<c>PUT</c>/<c>GET textbuilder</c>.
/// </summary>
public class TextBuilderJobResponse
{
    public int InvocationCount { get; set; } = 1;
    public string? Errors { get; set; }
}
