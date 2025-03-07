using API.Infrastructure.Validation;
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

    /// <summary>
    /// Whether the parent is in a flat or hierarichical form
    /// </summary>
    public static bool ParentIsFlatForm(this IPresentation presentation, string baseUrl, int customerId) =>
        presentation.Parent.ThrowIfNullOrEmpty(nameof(presentation.Parent))
            .StartsWith($"{baseUrl}/{customerId}/{SpecConstants.CollectionsSlug}");
}
