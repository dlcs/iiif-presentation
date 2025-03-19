using IIIF.Presentation.V3.Strings;

namespace Mapper.Entities
{
    public class DBCanvasPainting
    {
        /// <summary>
        /// Alphanumeric flat id from the manifests table: associates an asset with a manifest
        /// </summary>
        public required string ManifestId { get; set; }

        /// <summary>
        /// Canvases in Manifests always use a flat id, something like https://dlcs.io/iiif/canvases/ae4567rd, 
        /// rather than anything path-based. If requested directly, IIIF-CS returns canvas from this table with 
        /// partOf pointing at manifest(s). canvas_id might not be unique within this table if the asset is 
        /// painted in more than one Manifest
        /// </summary>
        public required string CanvasId { get; set; }

        /// <summary>
        /// A fully qualified external URL used when canvas_id is not managed; e.g., manifest was made externally.
        /// </summary>
        public string? CanvasOriginalId { get; set; }

        /// <summary>
        /// Canvas sequence order within a Manifest. This keeps incrementing for successive paintings 
        /// on the same canvas, it is always >= number of canvases in the manifest. For most manifests, 
        /// the number of rows equals the highest value of this. It stays the same for successive content 
        /// resources within a Choice (see choice_order). It gets recalculated on a Manifest save by 
        /// walking through the manifest.items, incrementing as we go.
        /// </summary>
        public int CanvasOrder { get; set; }

        /// <summary>
        /// Normally null; a positive integer indicates that the asset is part of a Choice body. 
        /// Multiple choice bodies share same value of order. When the successive content resources 
        /// are items in a Choice body, canvas_order holds constant and this row increments.
        /// </summary>
        public int? ChoiceOrder { get; set; }

        /// <summary>
        /// Platform asset ID (cust/space/id) to be painted on the canvas - may be null if external. 
        /// This is the resource that is the body (or one of the choice items), which may have further 
        /// services, adjuncts that the platform knows about. But we don't store the body JSON here, 
        /// and if it's not a platform asset, we don't have any record of the body - JSON is king.
        /// </summary>
        public string? AssetId { get; set; }

        /// <summary>
        /// If the painting annotation is not a platform-managed asset in this instance of the DLCS,
        /// store the Asset ID here.
        /// This may well be a parameterisation of an image service, but we'll just record it as an
        /// asset. The information is available for interrogation in the JSON.
        /// </summary>
        public string? ExternalAssetId { get; set; }

        /// <summary>
        /// As with manifest - URI of a 100px thumb. Could be derived from asset id though? 
        /// So may be null most of the time.
        /// </summary>
        public string? Thumbnail { get; set; }

        /// <summary>
        /// Stored language map, is the same as the on the canvas, may be null where it is not 
        /// contributing to the canvas, should be used for choice, multiples etc.
        /// (becomes JSON column in the DB)
        /// </summary>
        public LanguageMap? Label { get; set; }

        /// <summary>
        /// Only needed if the canvas label is not to be the first asset label; 
        /// multiple assets on a canvas use the first.
        /// </summary>
        public LanguageMap? CanvasLabel { get; set; }

        /// <summary>
        /// null if fills whole canvas, otherwise a parseable IIIF selector (fragment or JSON)
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// For images, the width of the image in the Manifest for which the IIIF API is a service. 
        /// This and static_height next two default to 0 in which case the largest thumbnail size 
        /// is used - which may be a secret thumbnail.
        /// </summary>
        public int? StaticWidth { get; set; }

        /// <summary>
        /// For images, the height of the image in the Manifest for which the IIIF API is a service.
        /// </summary>
        public int? StaticHeight { get; set; }


    }
}
