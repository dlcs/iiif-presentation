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
    /// Whether the parent is in a flat or hierarchical form
    /// </summary>
    public static bool ParentIsFlatForm(this IPresentation presentation, string baseUrl, int customerId) =>
        presentation.Parent.ThrowIfNullOrEmpty(nameof(presentation.Parent))
            .StartsWith($"{baseUrl}/{customerId}/{SpecConstants.CollectionsSlug}");
    
    /// <summary>
    /// Check if <see cref="IPresentation"/> objects publicId is customer root
    /// </summary>
    public static bool PublicIdIsRoot(this IPresentation presentation, string baseUrl, int customerId) =>
        presentation.PublicId.ThrowIfNullOrEmpty(nameof(presentation.PublicId)).Equals($"{baseUrl}/{customerId}");
}
