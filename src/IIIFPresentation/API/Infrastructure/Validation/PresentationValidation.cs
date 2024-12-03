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
    /// <param name="pathGenerator">Helps generate paths for collections</param>
    /// <returns>true if parent and invalid URI, else false</returns>
    public static bool IsUriParentInvalid(this IPresentation presentation, Collection parent, 
        IPathGenerator pathGenerator) 
        => Uri.IsWellFormedUriString(presentation.Parent, UriKind.Absolute)
           && !pathGenerator.GenerateFlatCollectionId(parent).Equals(presentation.Parent);
}