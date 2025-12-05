
using Models.Database;

namespace Services.Manifests.Helpers;

public static class CanvasPaintingX
{
    public static IOrderedEnumerable<T> OrderCanvasPaintings<T>(
        this IEnumerable<T> canvasPaintings) where T : CanvasPainting =>
        canvasPaintings.OrderBy(cp => cp.CanvasOrder).ThenBy(cp => cp.ChoiceOrder ?? 0);
}
