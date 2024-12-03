using System.Data;
using Core.Helpers;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Repository;

namespace API.Infrastructure.IdGenerator;

public class IdentityManager(
    IIdGenerator idGenerator,
    PresentationContext dbContext,
    ILogger<IdentityManager> logger)
{
    private const int MaxAttempts = 3;

    public async Task<string> GenerateUniqueId<T>(int customerId, CancellationToken cancellationToken = default)
        where T : class, IIdentifiable
    {
        var currentAttempt = 0;
        var random = new Random();

        while (currentAttempt < MaxAttempts)
        {
            var id = GenerateIdentity(customerId, random);

            var isUnique = !await dbContext.Set<T>()
                .AnyAsync(e => e.Id == id && e.CustomerId == customerId, cancellationToken);

            if (isUnique) return id;
            
            currentAttempt++;
        }

        throw new ConstraintException("Max attempts to generate an identifier exceeded");
    }

    public async Task<IList<string>> GenerateUniqueIds<T>(int customerId, int count,
        CancellationToken cancellationToken = default)
        where T : class, IIdentifiable
    {
        logger.LogTrace("Generating {IdCount} uniqueIds for {Type}", count, typeof(T).Name);
        
        var currentAttempt = 0;
        var random = new Random();
        var nonMatching = new List<string>(count);
        while (currentAttempt < MaxAttempts)
        {
            var requiredCount = count - nonMatching.Count;
            logger.LogTrace("Generating {IdCount} uniqueIds for {Type}, attempt {Attempt}", requiredCount,
                typeof(T).Name, currentAttempt);
            
            var candidates = Enumerable.Repeat(0, requiredCount)
                .Select(_ => GenerateIdentity(customerId, random))
                .ToList();
            var existingIds = await dbContext.Set<T>()
                .Where(i => candidates.Contains(i.Id) && i.CustomerId == customerId)
                .Select(i => i.Id)
                .ToListAsync(cancellationToken);

            // No matches in this batch, return the entire candidates list OR save the non-matching for next iteration
            if (existingIds.IsNullOrEmpty())
            {
                return currentAttempt == 0 ? candidates : candidates.Union(nonMatching).ToList();
            }
            
            nonMatching.AddRange(candidates.Except(existingIds));
            currentAttempt++;
        }
        
        throw new ConstraintException("Max attempts to generate an identifier exceeded");
    }

    private string GenerateIdentity(int customerId, Random random)
        => idGenerator.Generate([random.Next(0, int.MaxValue), DateTime.UtcNow.Ticks, customerId]);
}