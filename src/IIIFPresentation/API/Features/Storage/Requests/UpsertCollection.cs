using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using Core.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.Collection.Upsert;
using Models.Database.Collections;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage.Requests;

public class UpsertCollection(int customerId, string collectionId, UpsertFlatCollection collection, UrlRoots urlRoots, 
    string? eTag)
    : IRequest<ModifyEntityResult<FlatCollection>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; set; } = collectionId;

    public UpsertFlatCollection Collection { get; } = collection;

    public UrlRoots UrlRoots { get; } = urlRoots;
    
    public string? ETag { get; set; } = eTag;
}

public class UpsertCollectionHandler(
    PresentationContext dbContext,
    IETagManager eTagManager,
    ILogger<CreateCollection> logger,
    IOptions<ApiSettings> options)
    : IRequestHandler<UpsertCollection, ModifyEntityResult<FlatCollection>>
{
    private readonly ApiSettings settings = options.Value;

    private const int DefaultCurrentPage = 1;

    public async Task<ModifyEntityResult<FlatCollection>> Handle(UpsertCollection request, 
        CancellationToken cancellationToken)
    {
        var databaseCollection =
            await dbContext.Collections.FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken);

        if (databaseCollection == null)
        {
            if (request.ETag is not null)
            {
                return ModifyEntityResult<FlatCollection>.Failure(
                    "ETag should not be added when inserting collection via PUT", WriteResult.PreConditionFailed);
            }

            var createdDate = DateTime.UtcNow;
            
            var parentCollection = await dbContext.RetrieveCollection(request.CustomerId,
                request.Collection.Parent.GetLastPathElement(), cancellationToken);
            if (parentCollection == null)
            {
                return ModifyEntityResult<FlatCollection>.Failure(
                    "The parent collection could not be found", WriteResult.BadRequest);
            }

            databaseCollection = new Collection
            {
                Id = request.CollectionId,
                Created = createdDate,
                Modified = createdDate,
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
            
            await dbContext.AddAsync(databaseCollection, cancellationToken);
        }
        else
        {
            eTagManager.TryGetETag($"/{request.CustomerId}/collections/{request.CollectionId}", out var eTag);

            if (request.ETag != eTag)
            {
                return ModifyEntityResult<FlatCollection>.Failure(
                    "ETag does not match", WriteResult.PreConditionFailed);
            }

            if (databaseCollection.Parent != request.Collection.Parent)
            {
                var parentCollection = await dbContext.RetrieveCollection(request.CustomerId,
                    request.Collection.Parent.GetLastPathElement(), cancellationToken);

                if (parentCollection == null)
                {
                    return ModifyEntityResult<FlatCollection>.Failure(
                        $"The parent collection could not be found", WriteResult.BadRequest);
                }
            }

            databaseCollection.Modified = DateTime.UtcNow;
            databaseCollection.ModifiedBy = Authorizer.GetUser();
            databaseCollection.IsPublic = request.Collection.Behavior.IsPublic();
            databaseCollection.IsStorageCollection = request.Collection.Behavior.IsStorageCollection();
            databaseCollection.Label = request.Collection.Label;
            databaseCollection.Parent = request.Collection.Parent;
            databaseCollection.Slug = request.Collection.Slug;
            databaseCollection.Thumbnail = request.Collection.Thumbnail;
            databaseCollection.Tags = request.Collection.Tags;
            databaseCollection.ItemsOrder = request.Collection.ItemsOrder;
        }


        var saveErrors = await dbContext.TrySaveCollection(request.CustomerId, logger, cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }

        var total = await dbContext.Collections.CountAsync(
            c => c.CustomerId == request.CustomerId && c.Parent == databaseCollection.Id,
            cancellationToken: cancellationToken);

        var items = dbContext.Collections
            .Where(s => s.CustomerId == request.CustomerId && s.Parent == databaseCollection.Id)
            .Take(settings.PageSize);

        foreach (var item in items)
        { 
            item.FullPath = $"{(databaseCollection.Parent != null ? $"{databaseCollection.Slug}/" : string.Empty)}{item.Slug}";
        }

        if (databaseCollection.Parent != null)
        {
            databaseCollection.FullPath =
                CollectionRetrieval.RetrieveFullPathForCollection(databaseCollection, dbContext);
        }

        return ModifyEntityResult<FlatCollection>.Success(
            databaseCollection.ToFlatCollection(request.UrlRoots, settings.PageSize, DefaultCurrentPage, total,
                await items.ToListAsync(cancellationToken: cancellationToken)));
    }
}