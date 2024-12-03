using API.Infrastructure.Requests;
using Core;
using Models.API.General;

namespace API.Features.Manifest.Helpers;

public class ManifestErrorHelper
{
    public static ModifyEntityResult<TCollection, ModifyCollectionType> ParentMustBeStorageCollection<TCollection>()
        where TCollection : class
        => ModifyEntityResult<TCollection, ModifyCollectionType>.Failure("The parent must be a storage collection",
            ModifyCollectionType.ParentMustBeStorageCollection, WriteResult.Conflict);
}