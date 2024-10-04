using IIIF.Presentation.V3.Content;

namespace API.Features.Storage.Helpers;

public static class ThumbnailX
{
    private const int ThumbnailSize = 100;
    private const int AcceptableDelta = 2;
    
    public static string? GetThumbnailPath(this List<Image> thumbnails)
    {
        var byWidth = thumbnails.FirstOrDefault(i => i.Width.CloseEnough())?.Id;

        if (byWidth != null)
        {
            return byWidth;
        }
        
        var byHeight = thumbnails.FirstOrDefault(i => i.Height.CloseEnough())?.Id;

        return byHeight ?? thumbnails.MinBy(x => x.Width)?.Id;
    }
    
    private static bool CloseEnough(this int? toCheck)
    {
        if (toCheck.HasValue)
        {
            return Math.Abs(toCheck.Value - ThumbnailSize) <= AcceptableDelta;
        }
        
        return false;
    }
}