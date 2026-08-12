using IIIF.Presentation.V3.Strings;

namespace Mapper.DlcsApi
{
    public class CanvasPainting
    {
        public required string Canvas { get; set; }
        public int CanvasOrder { get; set; }
        public int? ChoiceOrder { get; set; }
        public LanguageMap? Label { get; set; }
        public string? ExternalAssetId { get; set; }
        public LanguageMap? CanvasLabel { get; internal set; }
    }
}
