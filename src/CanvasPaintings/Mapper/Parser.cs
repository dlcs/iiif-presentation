using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Serialisation;
using Mapper.DlcsApi;
using Mapper.Entities;
using Mapper.Identity;

namespace Mapper
{
    public class Parser
    {
        public List<DBCanvasPainting> ParseManifest(Manifest manifest)
        {
            var canvasPaintings = new List<DBCanvasPainting>();

            // We're only doing this because we're not simulating a Manifests table.
            // IRL this will be known and fixed.
            var manifestId = Identifiable.Generate(8);
            int canvasOrder = 0;

            foreach(var canvas in manifest.Items ?? [])
            {
                // similarly, we only do this when saving a Manifest for the first time,
                // or if we see a new Canvas in the incoming manifest.
                // Otherwise, we identify the incoming Canvas from its `id`,
                // which we match to either the canvas_id field OR the original_canvas_id field.
                var canvasId = Identifiable.Generate(8);

                foreach(var annoPage in canvas.Items ?? [])
                {
                    foreach (var anno in annoPage.Items ?? [])
                    {
                        if(anno is PaintingAnnotation painting)
                        {
                            var target = painting.Target;
                            var body = painting.Body;
                            if (body is PaintingChoice choice)
                            {
                                int constCanvasOrder = canvasOrder;
                                bool first = true;
                                int choiceOrder = 1; // (not -1; "a positive integer indicates that the asset is part of a Choice body.")

                                foreach (var choiceItem in choice.Items ?? [])
                                {
                                    var resource = choiceItem as ResourceBase;
                                    if (resource is SpecificResource specificResource)
                                    {
                                        resource = specificResource.Source as ResourceBase;
                                    }
                                    if (resource is Image || resource is Video || resource is Sound)
                                    {
                                        var cp = GetEntity(resource, manifestId, canvasId, canvas.Id!, constCanvasOrder, choiceOrder, target, canvas);
                                        if (cp != null)
                                        {
                                            choiceOrder++;
                                            if (first)
                                            {
                                                canvasOrder++;
                                                first = false;
                                            }
                                            target = null; // don't apply it to subsequent members of the choice
                                            canvasPaintings.Add(cp);


                                            // can't do this as PaintingChoice is not a Resourcebase - maybe it should be?
                                            // choiceCP.Label = resource?.Label ?? choice.Label ?? painting.Label ?? canvas.Label; 
                                            cp.Label = resource?.Label ?? painting.Label ?? canvas.Label;
                                            if (canvas.Label != null && canvas.Label != cp.Label && cp.CanvasLabel == null)
                                            {
                                                cp.CanvasLabel = canvas.Label;
                                            }
                                        }
                                        else
                                        {
                                            // body could be a Canvas - will need to handle that eventually but not right now
                                            // It is handled by unpacking the canvas into another loop through this
                                            throw new NotImplementedException("Not yet support canvases as painting anno bodies");
                                        }
                                    }
                                }
                            }
                            else if(body is Image || body is Video || body is Sound)
                            {
                                var resource = body as ResourceBase;
                                if (resource is SpecificResource specificResource)
                                {
                                    resource = specificResource.Source as ResourceBase;
                                }
                                var cp = GetEntity(resource, manifestId, canvasId, canvas.Id!, canvasOrder, null, target, canvas);
                                if (cp != null)
                                {
                                    canvasOrder++;
                                    canvasPaintings.Add(cp);
                                    cp.Label = resource?.Label ?? painting.Label ?? canvas.Label;
                                    if (canvas.Label != null && canvas.Label != cp.Label && cp.CanvasLabel == null)
                                    {
                                        cp.CanvasLabel = canvas.Label;
                                    }
                                }
                            }
                            else
                            {
                                throw new NotImplementedException("Not yet support canvases as painting anno bodies");
                            }
                        }
                    }
                }            
            }

            return canvasPaintings;
        }

        private DBCanvasPainting GetEntity(
            ResourceBase resource,
            string manifestId,
            string canvasId,
            string canvasOriginalId, 
            int canvasOrder,
            int? choiceOrder,
            IStructuralLocation? target,
            Canvas currentCanvas)
        {
            var cp = new DBCanvasPainting
            {
                ManifestId = manifestId,
                CanvasId = canvasId,
                CanvasOriginalId = canvasOriginalId, // do we always set this?
                CanvasOrder = canvasOrder,
                ChoiceOrder = choiceOrder,
                Target = TargetAsString(target, currentCanvas)
            };
            if (resource is ISpatial spatial)
            {
                cp.StaticWidth = spatial.Width;
                cp.StaticHeight = spatial.Height;
            }
            cp.AssetId = GetManagedAssetId(resource);
            return cp;
        }

        private string? GetManagedAssetId(ResourceBase media)
        {
            // obviously this is not hardcoded IRL
            const string DlcServices = "https://dlc.services/";
            const string DlcsIo = "https://dlcs.io/";

            if (media is Image || media is Video || media is Sound)
            {
                // How can we tell it's one of ours?
                // This logic may change in future, and we need to deal with rewritten assets.
                // We're going to assume that if the body path matches one of our derivative routes,
                // then it's ours, without checking to see if there's a service. 
                // We also need to extend this for born digital Wellcome pattern.
                var resource = media as ResourceBase;
                if(resource?.Id != null)
                {
                    if(resource.Id.StartsWith(DlcServices) || resource.Id.StartsWith(DlcsIo))
                    {
                        // Is in an assetty path?
                        var parts = resource.Id.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 6 && (parts[2] == "iiif-img" || parts[2] == "iiif-av" || parts[2] == "file"))
                        {
                            return $"{parts[3]}/{parts[4]}/{parts[5]}";
                        }
                    }
                }
            }
            return null;
        }

        private string? TargetAsString(IStructuralLocation? target, Canvas currentCanvas)
        {
            if (target == null) return null;
            if(target is Canvas canvas)
            {
                if(currentCanvas.Id == canvas.Id)
                {
                    // This indicates that we are targetting the whole canvas
                    return null;
                }
                return canvas.Id;
            }
            if(target is SpecificResource specificResource)
            {
                return specificResource.AsJson();
            }
            return null;
        }

        public List<PaintedResource> GetPaintedResources(List<DBCanvasPainting> entities)
        {
            return [];
        }
    }
}
