using Core.Helpers;
using Models.API;

namespace API.Helpers;

public static class PresentationX
{
    /// <summary>
    /// Gets the last path element of parent 
    /// </summary>
    public static string GetParentSlug(this IPresentation presentation) =>
        presentation.Parent.ThrowIfNullOrEmpty(nameof(presentation.Parent)).GetLastPathElement();
}