using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Validation;
using Models.Database.Collections;
using Repository.Paths;

namespace API.Features.Common.Helpers;

public static class ParentValidator
{
    /// <summary>
    /// Validates that a parent collection is not null or a IIIF collection
    /// </summary>
    public static PresentationResult? ValidateParentCollection(Collection? parentCollection)
    {
        if (parentCollection == null) return UpsertErrorHelper.NullParentResponse();

        return !parentCollection.IsStorageCollection ? UpsertErrorHelper.ParentMustBeStorageCollection() : null;
    }
}
