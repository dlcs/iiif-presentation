using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;

namespace Models.API.Collection;

public class PartOf(string type) : ResourceBase
{
    #region Overrides of ResourceBase

    public override string Type { get; } = type;

    #endregion
}