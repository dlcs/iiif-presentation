using API.Helpers;
using Core.Helpers;
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
    /// <param name="customerId">The customer id of the request</param>
    /// <returns>true if parent and invalid URI, else false</returns>
    public static bool IsParentInvalid(this IPresentation presentation, Collection parent, int customerId)
    {
        if (presentation.Parent == null) return false;
        if (parent.CustomerId != customerId) return false;

        return presentation.ParentIsFlatForm()
            ? !parent.Id.Equals(presentation.Parent.GetLastPathElement())
            : !parent.Hierarchy.GetCanonical().Slug.Equals(PathParser.GetSlugFromHierarchicalPath(presentation.Parent, customerId));
    }
}
