using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;

namespace BackgroundHandler.Helpers;

public static class ManifestX
{
    public static PaintingAnnotation? GetFirstPaintingAnnotation(this Canvas canvas)
    {
        return canvas.Items?[0].Items?[0] as PaintingAnnotation;
    }
    
    public static PaintingAnnotation? GetFirstPaintingAnnotation(this List<AnnotationPage> annotationPages)
    {
        return annotationPages[0].Items?[0] as PaintingAnnotation;
    }
    
    public static PaintingAnnotation? GetFirstPaintingAnnotation(this AnnotationPage annotationPages)
    {
        return annotationPages.Items?[0] as PaintingAnnotation;
    }
    
    public static AnnotationPage? GetFirstAnnotationPage(this Canvas canvas)
    {
        return canvas.Items?[0];
    }
    
    public static PaintingAnnotation GetPaintingAnno(this Canvas canvas, int index)
        => canvas.Items[0].Items?[index] as PaintingAnnotation;
    
    public static AnnotationPage GetCanvasAnnotationPage(this Manifest manifest, int index)
    {
        return manifest.Items![index].Items![0];
    }

    /// <summary>
    /// Returns true if Duration, Width and Height are all unset
    /// </summary>
    public static bool DimensionsAreUnset(this Canvas canvas) =>
        !canvas.Width.HasValue && !canvas.Height.HasValue && !canvas.Duration.HasValue;

    public static (int width, int height)? GetCanvasDimensions(this Canvas canvas)
    {
        switch (canvas.GetFirstPaintingAnnotation()?.Body)
        {
            case null:
                // Just get from the services or from canvas itself as fallback
                if (canvas.Service.GetItemDimensionsFromServices() is { } canvasDimensions)
                    return canvasDimensions;
                return canvas is {Width: { } cWidth, Height: { } cHeight}
                    ? (cWidth, cHeight)
                    : null;

            case PaintingChoice choice:
                // Again, try first from services
                if (choice.Service.GetItemDimensionsFromServices() is { } choiceDimensions)
                    return choiceDimensions;

                // otherwise find like, first image with dimensions, if any
                return (choice.Items?.OfType<Image>()
                        .FirstOrDefault(x => x is {Width: not null, Height: not null}))
                    .GetItemDimensionsFromImage();

            case Image image:
                return image.GetItemDimensionsFromImage();

            default: return null;
        }
    }
}
