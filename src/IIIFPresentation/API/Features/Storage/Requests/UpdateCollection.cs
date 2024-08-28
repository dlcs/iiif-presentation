using API.Converters;
using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.Infrastucture;
using Repository;

namespace API.Features.Storage.Requests;

public class UpdateCollection(int customerId, string collectionId, FlatCollection collection, UrlRoots urlRoots)
    : IRequest<ModifyEntityResult<FlatCollection>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; set; } = collectionId;

    public FlatCollection Collection { get; } = collection;

    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class UpdateCollectionHandler(
    PresentationContext dbContext,
    ILogger<CreateCollection> logger,
    IOptions<ApiSettings> options)
    : IRequestHandler<UpdateCollection, ModifyEntityResult<FlatCollection>>
{
    private readonly ApiSettings settings = options.Value;

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
        collectionFromDatabase.ModifiedBy = GetUser(); //TODO: update this to get a real user
        collectionFromDatabase.IsPublic = request.Collection.Behavior.Contains(Behavior.IsPublic);
        collectionFromDatabase.IsStorageCollection =
            request.Collection.Behavior.Contains(Behavior.IsStorageCollection);
        collectionFromDatabase.ItemsOrder = request.Collection.ItemsOrder;
        collectionFromDatabase.Label = request.Collection.Label;
        collectionFromDatabase.Parent = request.Collection.Parent!.Split('/').Last();
        collectionFromDatabase.Slug = request.Collection.Slug;
        collectionFromDatabase.Thumbnail = request.Collection.Thumbnail;
        collectionFromDatabase.Tags = request.Collection.Tags;
        
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex,"Error updating collection for customer {Customer} in the database", request.CustomerId);

            if (ex.InnerException != null && ex.InnerException.Message.Contains("duplicate key value violates unique constraint \"ix_collections_customer_id_slug_parent\""))
            {
                return ModifyEntityResult<FlatCollection>.Failure(
                    "The collection could not be created due to a duplicate slug value", WriteResult.BadRequest);
            }
            
            return ModifyEntityResult<FlatCollection>.Failure(
                "The collection could not be created");
        }

        var items = dbContext.Collections.Where(s => s.CustomerId == request.CustomerId && s.Parent == collectionFromDatabase.Id);

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
            collectionFromDatabase.ToFlatCollection(request.UrlRoots, settings.PageSize, items));
    }

    private string? GetUser()
    {
        return "Admin";
    }
}