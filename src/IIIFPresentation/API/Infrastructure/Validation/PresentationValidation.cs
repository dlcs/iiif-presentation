using API.Converters;
using API.Helpers;
using Models.API;
using Models.Database.Collections;

namespace API.Infrastructure.Validation;

public static class PresentationValidation
{
    /// <summary>
    /// If parent is full URI, verify it indeed is pointing to the resolved parent collection
    /// </summary>
    /// <param name="presentation">Current <see cref="IPresentation"/> object</param>
    /// <param name="parent">Parent <see cref="Collection"/> object</param>
    /// <param name="urlRoots">Current <see cref="UrlRoots"/> object</param>
    /// <returns>true if parent and invalid URI, else false</returns>
    public static bool IsUriParentInvalid(this IPresentation presentation, Collection parent, UrlRoots urlRoots) 
        => Uri.IsWellFormedUriString(presentation.Parent, UriKind.Absolute)
           && !parent.GenerateFlatCollectionId(urlRoots).Equals(presentation.Parent);
}