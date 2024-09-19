using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using Core.Helpers;
using MediatR;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.Collection.Upsert;
using Models.Database.Collections;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage.Requests;

public class CreateCollection(int customerId, UpsertFlatCollection collection, UrlRoots urlRoots)
    : IRequest<ModifyEntityResult<FlatCollection>>
{
    public int CustomerId { get; } = customerId;

    public UpsertFlatCollection Collection { get; } = collection;

    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class CreateCollectionHandler(
    PresentationContext dbContext,
    ILogger<CreateCollection> logger,
    IOptions<ApiSettings> options)
    : IRequestHandler<CreateCollection, ModifyEntityResult<FlatCollection>>
{
    private readonly ApiSettings settings = options.Value;

    private const int CurrentPage = 1;
    
    public async Task<ModifyEntityResult<FlatCollection>> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
        // check parent exists
        var parentCollection = await dbContext.RetrieveCollection(request.CustomerId,
            request.Collection.Parent.GetLastPathElement(), RootCollection.Id, cancellationToken);

        if (parentCollection == null)
        {
            return ModifyEntityResult<FlatCollection>.Failure(
                $"The parent collection could not be found", WriteResult.Conflict);
        }
        
        var collection = new Collection()
        {
            Id = Guid.NewGuid().ToString(),
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = request.CustomerId,
            IsPublic = request.Collection.Behavior.IsPublic(),
            IsStorageCollection = request.Collection.Behavior.IsStorageCollection(),
            Label = request.Collection.Label,
            Parent = parentCollection.Id,
            Slug = request.Collection.Slug,
            Thumbnail = request.Collection.Thumbnail,
            Tags = request.Collection.Tags,
            ItemsOrder = request.Collection.ItemsOrder
        };

        dbContext.Collections.Add(collection);

        var saveErrors = await dbContext.TrySaveCollection(request.CustomerId, logger, cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }
        
        if (collection.Parent != null)
        {
            collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        }

        collection.UpdateParentForRootIfRequired(request.Collection.Parent);

        return ModifyEntityResult<FlatCollection>.Success(
            collection.ToFlatCollection(request.UrlRoots, settings.PageSize, CurrentPage, 0, []), // there can be no items attached to this, as it's just been created
            WriteResult.Created);
    }
}