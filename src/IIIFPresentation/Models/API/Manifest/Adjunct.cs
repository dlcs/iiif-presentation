using System.Text.Json.Serialization;
using IIIF.Presentation.V3.Strings;
using Models.DLCS;

namespace Models.API.Manifest;

public class Adjunct
{
    public required string Id { get; set; }
    
    /// <summary>
    /// The internet content type (or MIME type) of the resource
    /// </summary>
    public required string MediaType { get; set; }
    
    /// <summary>
    /// How this adjunct is expressed in IIIF presentation
    /// </summary>
    public required IIIFLinkType IIIFLink { get; set; }
    
    /// <summary>
    /// The asset id this adjunct is associated with
    /// </summary>
    [JsonPropertyName("adjunct")]
    public AssetId? AssetId { get; set; }
    
    /// <summary>
    /// The type of the adjunct
    /// </summary>
    [JsonPropertyName("@type")]
    public required string Type { get; set; }
    
    /// <summary>
    /// A schema or named set of functionality available from the resource
    /// </summary>
    public string? Profile { get; set; }
    
    /// <summary>
    /// A human-readable label, name or title
    /// </summary>
    public LanguageMap? Label { get; set; }
    
    /// <summary>
    /// The language(s) of the content
    /// </summary>
    public string[]? Language { get; set; }
    
    /// <summary>
    /// A fully-qualified URL external to the platform where the adjunct is hosted
    /// </summary>
    public Uri? ExternalId { get; set; }

    /// <summary>
    /// The location of the file used to generate a IIIF-CS adjunct
    /// </summary>
    public string? Origin { get; set; }
    
    /// <summary>
    /// When the adjunct last finished processing
    /// </summary>
    public DateTime? Finished { get; set; }
    
    /// <summary>
    /// The size in bytes of the adjunct
    /// </summary>
    public long? Size { get; set; }
    
    /// <summary>
    /// The reason why this adjunct exists.
    /// </summary>
    public string? Motivation { get; set; }
    
    /// <summary>
    /// An additional set of features or functionality this adjunct provides
    /// </summary>
    public string? Provides { get; set; }
}
