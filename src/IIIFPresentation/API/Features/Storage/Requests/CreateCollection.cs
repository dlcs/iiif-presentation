using API.Infrastructure.Requests;
using MediatR;
using Models.Response;

namespace API.Features.Storage.Requests;

public class CreateCollection : IRequest<ModifyEntityResult<FlatCollection>>
{
    public CreateCollection(int customerId, FlatCollection collection)
    {
        CustomerId = customerId;
        Collection = collection;
    }

    public int CustomerId { get; }
    
    public FlatCollection Collection { get; }
}

public class CreateCollectionHandler : IRequestHandler<CreateCollection, ModifyEntityResult<FlatCollection>>
{
    public Task<ModifyEntityResult<FlatCollection>> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}