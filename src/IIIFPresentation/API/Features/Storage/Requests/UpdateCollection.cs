using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using Core.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.Collection.Upsert;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage.Requests;

public class UpdateCollection(int customerId, string collectionId, UpsertFlatCollection collection, UrlRoots urlRoots)
    : IRequest<ModifyEntityResult<FlatCollection>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; set; } = collectionId;

    public UpsertFlatCollection Collection { get; } = collection;

    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class UpdateCollectionHandler(
    PresentationContext dbContext,
    ILogger<CreateCollection> logger,
    IOptions<ApiSettings> options)
    : IRequestHandler<UpdateCollection, ModifyEntityResult<FlatCollection>>
{
    private readonly ApiSettings settings = options.Value;

    private const int DefaultCurrentPage = 1;

    public async Task<ModifyEntityResult<FlatCollection>> Handle(UpdateCollection request, CancellationToken cancellationToken)
    {
        var collectionFromDatabase =
            await dbContext.Collections.FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken);

        if (collectionFromDatabase == null)
        {
            return ModifyEntityResult<FlatCollection>.Failure(
                "Could not find a matching record for the provided collection id", WriteResult.NotFound);
        }

        collectionFromDatabase.Modified = DateTime.UtcNow;
        collectionFromDatabase.ModifiedBy = Authorizer.GetUser();
        collectionFromDatabase.IsPublic = request.Collection.Behavior.IsPublic();
        collectionFromDatabase.IsStorageCollection = request.Collection.Behavior.IsStorageCollection();
        collectionFromDatabase.Label = request.Collection.Label;
        collectionFromDatabase.Parent = request.Collection.Parent.GetLastPathElement();
        collectionFromDatabase.Slug = request.Collection.Slug;
        collectionFromDatabase.Thumbnail = request.Collection.Thumbnail;
        collectionFromDatabase.Tags = request.Collection.Tags;
        collectionFromDatabase.ItemsOrder = request.Collection.ItemsOrder;
        
        var saveErrors = await dbContext.TrySaveCollection(request.CustomerId, logger, cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }

        var total = await dbContext.Collections.CountAsync(
            c => c.CustomerId == request.CustomerId && c.Parent == collectionFromDatabase.Id,
            cancellationToken: cancellationToken);

        var items = dbContext.Collections
            .Where(s => s.CustomerId == request.CustomerId && s.Parent == collectionFromDatabase.Id)
            .Take(settings.PageSize);

        foreach (var item in items)
        { 
            item.FullPath = $"{(collectionFromDatabase.Parent != null ? $"{collectionFromDatabase.Slug}/" : string.Empty)}{item.Slug}";
        }

        if (collectionFromDatabase.Parent != null)
        {
            collectionFromDatabase.FullPath =
                CollectionRetrieval.RetrieveFullPathForCollection(collectionFromDatabase, dbContext);
        }

        return ModifyEntityResult<FlatCollection>.Success(
            collectionFromDatabase.ToFlatCollection(request.UrlRoots, settings.PageSize, DefaultCurrentPage, total,
                await items.ToListAsync(cancellationToken: cancellationToken)));
    }
}