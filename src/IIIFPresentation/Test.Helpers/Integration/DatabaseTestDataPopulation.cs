using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Models.Database.Collections;

namespace Test.Helpers.Integration;

public static class DatabaseTestDataPopulation
{
    public static ValueTask<EntityEntry<Collection>> AddTestCollection(this DbSet<Collection> collections)
    {
        return collections.AddAsync(new Collection
        {
            Id = "RootStorage",
            Slug = "1",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", new List<string> {"repository root"}}
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = true,
            CustomerId = 1
        });
    }
}