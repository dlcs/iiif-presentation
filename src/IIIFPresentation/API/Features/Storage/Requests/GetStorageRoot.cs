using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Repository;

namespace API.Features.Storage.Requests;

public class GetStorageRoot : IRequest<(Collection? root, IQueryable<Collection>? items)>
{
    public GetStorageRoot(int customerId)
    {
        CustomerId = customerId;
    }

    public int CustomerId { get; }
}

public class GetStorageRootHandler : IRequestHandler<GetStorageRoot, (Collection? root, IQueryable<Collection>? items)>
{
    private readonly PresentationContext dbContext;

    public GetStorageRootHandler(PresentationContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<(Collection? root, IQueryable<Collection>? items)> Handle(GetStorageRoot request,
        CancellationToken cancellationToken)
    {
        var storage = await dbContext.Collections.AsNoTracking().FirstOrDefaultAsync(
            s => s.CustomerId == request.CustomerId && s.Parent == null,
            cancellationToken);

        IQueryable<Collection> items = null;

        if (storage != null)
        {
            items = dbContext.Collections.Where(s => s.CustomerId == request.CustomerId && s.Parent == storage.Id);

            foreach (var item in items)
            {
                item.FullPath = $"{storage.Slug}/{item.Slug}";
            }
        }

        return (storage, items);
    }
}