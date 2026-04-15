using System.ComponentModel;

namespace Models.DLCS;

/// <summary>
/// The type of linking property to use for adjunct. Determines how this is output on Manifest.
/// </summary>
public enum IIIFLinkType
{
    [Description("seeAlso")]
    SeeAlso,
    [Description("annotations")]
    Annotations,
    [Description("rendering")]
    Rendering,
    [Description("inlineAnnotation")]
    InlineAnnotation
}
