using System.Data;
using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Helpers;
using API.Infrastructure.AWS;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using Core.Helpers;
using MediatR;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.General;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Collection = Models.Database.Collections.Collection;
using IIdGenerator = API.Infrastructure.IdGenerator.IIdGenerator;

namespace API.Features.Storage.Requests;

public class CreateCollection(int customerId, PresentationCollection collection, string rawRequestBody, UrlRoots urlRoots)
    : IRequest<ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public PresentationCollection? Collection { get; } = collection;
    
    public string RawRequestBody { get; } = rawRequestBody;

    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class CreateCollectionHandler(
    PresentationContext dbContext,
    ILogger<CreateCollectionHandler> logger,
    IIIFS3Service iiifS3,
    IIdGenerator idGenerator,
    IOptions<ApiSettings> options)
    : IRequestHandler<CreateCollection, ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;

    private const int CurrentPage = 1;
    
    public async Task<ModifyEntityResult<PresentationCollection, ModifyCollectionType>> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
        var isStorageCollection = request.Collection.Behavior.IsStorageCollection();
        TryConvertIIIFResult<IIIF.Presentation.V3.Collection>? iiifCollection = null;
        if (!isStorageCollection)
        {
            iiifCollection = request.RawRequestBody.ConvertCollectionToIIIF<IIIF.Presentation.V3.Collection>(logger);
            if (iiifCollection.Error) return ErrorHelper.CannotValidateIIIF<PresentationCollection>();
        }
        
        // check parent exists
        var parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
            request.Collection!.Parent.GetLastPathElement(), cancellationToken: cancellationToken);

        if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();

        // If full URI was used, verify it indeed is pointing to the resolved parent collection
        if (Uri.IsWellFormedUriString(request.Collection.Parent, UriKind.Absolute)
            && !parentCollection.GenerateFlatCollectionId(request.UrlRoots).Equals(request.Collection.Parent))
            return ErrorHelper.NullParentResponse<PresentationCollection>();
        
        string id;

        try
        {
            id = await dbContext.Collections.GenerateUniqueIdAsync(request.CustomerId, idGenerator, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
            return ErrorHelper.CannotGenerateUniqueId<PresentationCollection>();
        }

        var dateCreated = DateTime.UtcNow;
        var collection = new Collection
        {
            Id = id,
            Created = dateCreated,
            Modified = dateCreated,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = request.CustomerId,
            Tags = request.Collection.Tags,
            IsPublic = request.Collection.Behavior.IsPublic(),
            IsStorageCollection = isStorageCollection,
            Label = request.Collection.Label,
            Thumbnail = request.Collection.GetThumbnail(),
        };

        var hierarchy = new Hierarchy
        {
            CollectionId = id,
            Type = isStorageCollection
                ? ResourceType.StorageCollection
                : ResourceType.IIIFCollection,
            Slug = request.Collection.Slug,
            CustomerId = request.CustomerId,
            Canonical = true,
            ItemsOrder = request.Collection.ItemsOrder,
            Parent = parentCollection.Id
        };

        dbContext.Collections.Add(collection);
        dbContext.Hierarchy.Add(hierarchy);

        var saveErrors =
            await dbContext.TrySaveCollection<PresentationCollection>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }

        await UploadToS3IfRequiredAsync(collection, iiifCollection?.ConvertedIIIF, request.UrlRoots,
            isStorageCollection, cancellationToken);

        if (hierarchy.Parent != null)
        {
            collection.FullPath =
                await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
        }
        
        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(
            collection.ToFlatCollection(request.UrlRoots, settings.PageSize, CurrentPage, 0, Enumerable.Empty<Hierarchy>()), // there can be no items attached to this, as it's just been created
            WriteResult.Created);
    }

    private async Task UploadToS3IfRequiredAsync(Collection collection, IIIF.Presentation.V3.Collection? iiifCollection, 
        UrlRoots urlRoots, bool isStorageCollection, CancellationToken cancellationToken = default)
    {
        if (!isStorageCollection)
        {
            await iiifS3.SaveIIIFToS3(iiifCollection!, collection, collection.GenerateFlatCollectionId(urlRoots),
                cancellationToken);
        }
    }
}