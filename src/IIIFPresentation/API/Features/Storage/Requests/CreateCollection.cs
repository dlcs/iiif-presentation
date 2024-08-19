using API.Converters;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.Database.Collections;
using Models.Infrastucture;
using Repository;

namespace API.Features.Storage.Requests;

public class CreateCollection : IRequest<ModifyEntityResult<FlatCollection>>
{
    public CreateCollection(int customerId, FlatCollection collection, UrlRoots urlRoots)
    {
        CustomerId = customerId;
        Collection = collection;
        UrlRoots = urlRoots;
    }

    public int CustomerId { get; }
    
    public FlatCollection Collection { get; }
    
    public UrlRoots UrlRoots { get; }
}

public class CreateCollectionHandler : IRequestHandler<CreateCollection, ModifyEntityResult<FlatCollection>>
{
    private readonly PresentationContext dbContext;
    
    private readonly ILogger<CreateCollection> logger;
    
    private readonly ApiSettings settings;

    public CreateCollectionHandler(
        PresentationContext dbContext, 
        ILogger<CreateCollection> logger, 
        IOptions<ApiSettings> options)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        settings = options.Value;
    }
    
    public async Task<ModifyEntityResult<FlatCollection>> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
        var collection = new Collection()
        {
            Id = Guid.NewGuid().ToString(),
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = GetUser(), //TODO: update this to get a real user
            CustomerId = request.CustomerId,
            IsPublic = request.Collection.Behavior.Contains(Behavior.IsPublic),
            IsStorageCollection = request.Collection.Behavior.Contains(Behavior.IsStorageCollection),
            ItemsOrder = request.Collection.ItemsOrder,
            Label = request.Collection.Label,
            Parent = request.Collection.Parent!.Split('/').Last(),
            Slug = request.Collection.Slug,
            Thumbnail = request.Collection.Thumbnail,
            Tags = request.Collection.Tags
        };

        dbContext.Collections.Add(collection);
        
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex,"Error creating collection");
            
            return ModifyEntityResult<FlatCollection>.Failure(
                $"The collection could not be created");
        }

        return ModifyEntityResult<FlatCollection>.Success(
            collection.ToFlatCollection(request.UrlRoots, settings.PageSize, 
                new EnumerableQuery<Collection>(new List<Collection>())), // there can be no items attached to this as it's just been created
            WriteResult.Created);
    }

    private string? GetUser()
    {
        return "Admin";
    }
}