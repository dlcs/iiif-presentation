using IIIF.Presentation.V3.Content;

namespace BackgroundHandler.Helpers;

public static class ImageX
{
    public static (int width, int height)? GetItemDimensionsFromImage(this Image? image) =>
        image switch
        {
            null => null,
            not null when image.Service.GetItemDimensionsFromServices() is { } imageDimensions => imageDimensions,
            {Width: { } iWidth, Height: { } iHeight} => (iWidth, iHeight),
            _ => null
        };
}
