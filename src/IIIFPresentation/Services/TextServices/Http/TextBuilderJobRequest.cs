namespace Services.TextServices.Http;

/// <summary>
/// Body for text-services' <c>POST</c>/<c>PUT textbuilder</c> - a subset of its <c>JobInstruction</c>.
/// </summary>
public class TextBuilderJobRequest(TextJobId id, string sourceUri, JobServices services, string? pdfCoverPage)
{
    public string Id { get; set; } = id.ToString();

    public string SourceUri { get; set; } = sourceUri;

    public int Services { get; set; } = (int)services;

    /// <summary>
    /// Pages inserted into the <see cref="SourceUri"/> manifest. A cover page is a pdf-type page inserted before the
    /// first canvas.
    /// </summary>
    public List<TextBuilderPage>? SourceData { get; set; } = pdfCoverPage == null
        ? null
        : [new TextBuilderPage { Type = "pdf", Input = pdfCoverPage, Index = 0 }];
}

/// <summary>
/// An entry in <see cref="TextBuilderJobRequest.SourceData"/> - a subset of text-services' <c>PageInstruction</c>.
/// </summary>
public class TextBuilderPage
{
    public required string Type { get; set; }

    public required string Input { get; set; }

    public int Index { get; set; }
}
