using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
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

    public async Task<ModifyEntityResult<FlatCollection>> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
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
            Parent = request.Collection.Parent!.GetLastPathElement(),
            Slug = request.Collection.Slug,
            Thumbnail = request.Collection.Thumbnail,
            Tags = request.Collection.Tags
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

        return ModifyEntityResult<FlatCollection>.Success(
            collection.ToFlatCollection(request.UrlRoots, settings.PageSize, 
                new EnumerableQuery<Collection>([])), // there can be no items attached to this, as it's just been created
            WriteResult.Created);
    }
}