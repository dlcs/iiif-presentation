using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;

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
    
    public static AnnotationPage GetCurrentCanvasAnnotationPage(this Manifest manifest, int index)
    {
        return manifest.Items![index].Items![0];
    }
}
