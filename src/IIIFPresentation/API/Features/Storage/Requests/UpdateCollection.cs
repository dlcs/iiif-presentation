using API.Converters;
using API.Infrastructure.Requests;
using MediatR;
using Models.API.Collection;

namespace API.Features.Storage.Requests;

public class UpdateCollection : IRequest<ModifyEntityResult<FlatCollection>>
{
    public UpdateCollection(int customerId, string collectionId, FlatCollection collection, UrlRoots urlRoots)
    {
        CustomerId = customerId;
        CollectionId = collectionId;
        Collection = collection;
        UrlRoots = urlRoots;
    }

    public int CustomerId { get; }
    
    public string CollectionId { get; set; }
    
    public FlatCollection Collection { get; }
    
    public UrlRoots UrlRoots { get; }
}