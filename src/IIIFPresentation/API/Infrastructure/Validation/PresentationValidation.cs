using API.Helpers;
using Models.API;
using Models.Database.Collections;
using Repository.Helpers;
using Repository.Paths;

namespace API.Infrastructure.Validation;

public static class PresentationValidation
{
    /// <summary>
    /// If parent is full URI, verify it indeed is pointing to the resolved parent collection
    /// </summary>
    /// <param name="presentation">Current <see cref="IPresentation"/> object</param>
    /// <param name="parent">Parent <see cref="Collection"/> object</param>
    /// <param name="baseUri">The base uri used to validate whether the parent is valid</param>
    /// <param name="customerId">The customer id</param>
    /// <param name="pathGenerator">Helps generate paths for collections</param>
    /// <returns>true if parent and invalid URI, else false</returns>
    public static bool IsParentInvalid(this IPresentation presentation, Collection parent, string baseUri, int customerId,
        IPathGenerator pathGenerator)
    {
        if (presentation.Parent == null) return false;
        
        return presentation.ParentIsFlatForm(baseUri, customerId) ? !pathGenerator.GenerateFlatCollectionId(parent).Equals(presentation.Parent) :
            !pathGenerator.GenerateHierarchicalId(parent.Hierarchy.GetCanonical()).Equals(presentation.Parent);
    }
}
