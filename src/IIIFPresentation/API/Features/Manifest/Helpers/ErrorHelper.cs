using API.Infrastructure.Requests;
using Core;
using IIIF;
using Models.API.General;

namespace API.Features.Manifest.Helpers;

public class ManifestErrorHelper
{
    public static ModifyEntityResult<TCollection, ModifyCollectionType> ParentMustBeStorageCollection<TCollection>()
        where TCollection : JsonLdBase
        => ModifyEntityResult<TCollection, ModifyCollectionType>.Failure("The parent must be a storage collection",
            ModifyCollectionType.ParentMustBeStorageCollection, WriteResult.Conflict);
}
