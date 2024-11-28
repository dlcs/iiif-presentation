using IIIF;
using IIIF.Presentation.V3.Content;

namespace Core.IIIF;

public static class ThumbnailX
{
    private const int ThumbnailSize = 100;
    
    /// <summary>
    /// Finds the thumbnail path of the closest thumbnail to a set size
    /// </summary>
    /// <param name="thumbnails">List of thumbnails to check for closest</param>
    /// <returns>The id of the closest thumbnail</returns>
    public static string? GetThumbnailPath(this IEnumerable<Image> thumbnails)
    {
        return thumbnails.SizeClosestTo(ThumbnailSize).Id;
    }
    
    /// <summary>
    /// From provided sizes, return the Size that has MaxDimension closest to specified targetSize
    ///
    /// e.g. [[100, 200], [250, 500] [500, 1000]], targetSize = 800 would return [500, 1000]
    /// </summary>
    /// <param name="sizes">List of sizes to query</param>
    /// <param name="targetSize">Ideal MaxDimension to find</param>
    /// <returns><see cref="Size"/> closes to specified value</returns>
    private static Image SizeClosestTo(this IEnumerable<Image> sizes, int targetSize)
    {
        var closestSize = sizes
            .OrderBy(MaxDimension)
            .Aggregate((x, y) =>
                Math.Abs(x.MaxDimension() - targetSize) < Math.Abs(y.MaxDimension() - targetSize) ? x : y);
        return closestSize;
    }

    private static int MaxDimension(this Image s)
    {
        return s.Width > s.Height ? s.Width ?? 0 : s.Height ?? 0;
    }
}