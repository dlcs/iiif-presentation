using System.Data;
using API.Converters;
using API.Infrastructure.IdGenerator;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Models.Database.Collections;
using Models.Database.General;

namespace API.Helpers;

/// <summary>
/// Collection of helpers to generate paths etc. for collections
/// </summary>
public static class CollectionHelperX
{
    private const int MaxAttempts = 3;
    private const string ManifestsSlug = "manifests";
    private const string CollectionsSlug = "collections";

    /// <summary>
    /// Get the ETag cache-key for resource 
    /// </summary>
    public static string GenerateETagCacheKey(this IHierarchyResource hierarchyResource)
        => $"/{hierarchyResource.CustomerId}/{hierarchyResource.GetSlug()}/{hierarchyResource.Id}";

    private static string GetSlug<T>(this T resource) where T : IHierarchyResource
        => resource is Manifest ? ManifestsSlug : CollectionsSlug;

    [Obsolete("Use IdentityManager.GenerateUniqueId instead.")]
    public static async Task<string> GenerateUniqueIdAsync<T>(this DbSet<T> entities,
        int customerId, IIdGenerator idGenerator, CancellationToken cancellationToken = default)
        where T : class, IIdentifiable
    {
        var isUnique = false;
        var id = string.Empty;
        var currentAttempt = 0;
        var random = new Random();
        var maxRandomValue = 25000;

        while (!isUnique)
        {
            if (currentAttempt > MaxAttempts)
            {
                throw new ConstraintException("Max attempts to generate an identifier exceeded");
            }

            id = idGenerator.Generate([
                customerId,
                DateTime.UtcNow.Ticks,
                random.Next(0, maxRandomValue)
            ]);

            isUnique = !await entities.AnyAsync(e => e.Id == id && e.CustomerId == customerId, cancellationToken);

            currentAttempt++;
        }

        return id;
    }
}
