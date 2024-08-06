using MediatR;
using Microsoft.EntityFrameworkCore;
using Repository;

namespace API.Features.Storage.Requests;

public class GetStorageRoot : IRequest<Models.Database.Collections.Collection>
{
    public GetStorageRoot(int customerId)
    {
        CustomerId = customerId;
    }

    public int CustomerId { get; }
}

public class GetStorageRootHandler : IRequestHandler<GetStorageRoot, Models.Database.Collections.Collection?>
{
    private readonly PresentationContext dbContext;

    public GetStorageRootHandler(PresentationContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Models.Database.Collections.Collection?> Handle(GetStorageRoot request, CancellationToken cancellationToken)
    {
        var storage = await dbContext.Collections.FirstOrDefaultAsync(s => s.CustomerId == request.CustomerId && s.Parent == null,
            cancellationToken);
        
        return storage;
    }
}