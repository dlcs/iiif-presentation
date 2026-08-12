namespace Mapper.DlcsApi
{
    public class PseudoManifest
    {
        public required string Id { get; set; }
        public string Type => "Manifest";

        public List<PaintedResource> PaintedResources { get; set; } = [];
    }
}
