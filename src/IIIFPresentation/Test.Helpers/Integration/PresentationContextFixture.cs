#nullable disable

using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Test.Helpers.Helpers;
using Testcontainers.PostgreSql;

namespace Test.Helpers.Integration;

public class PresentationContextFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgresContainer;
    
    public PresentationContext DbContext { get; private set; }
    public string ConnectionString { get; private set; }
    
    /// <summary>
    /// Identity of default seeded customer
    /// </summary>
    public const int CustomerId = 1;
    
    public PresentationContextFixture()
    {
        var postgresBuilder = new PostgreSqlBuilder()
            .WithImage("postgres:14")
            .WithDatabase("db")
            .WithUsername("postgres")
            .WithPassword("postgres_pword")
            .WithCleanUp(true)
            .WithLabel("presentation_test", "True");

        postgresContainer = postgresBuilder.Build();
    }

    private async Task SeedCustomer()
    {
        /* This will create
         * - root/
         *   - FirstChildCollection/
         *     - SecondChildCollection/
         *   - NonPublic/
         *   - IiifCollection/
         *   - IiifCollectionWithItems/
         *   - FirstChildManifest/
         *   - FirstChildManifestProcessing/
         */
        
        // Root collection
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = RootCollection.Id,
            UsePath = true,
            Label = new LanguageMap
            {
                { "en", ["repository root"] }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = true,
            CustomerId = CustomerId,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "",
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        });

        // Child Storage collection
        await DbContext.Collections.AddAsync(new Collection
        {
            Id = "FirstChildCollection",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", ["first child"] }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = true,
            CustomerId = CustomerId,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "first-child",
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        });

        // Grandchild storage collection
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = "SecondChildCollection",
            UsePath = true,
            Label = new LanguageMap
            {
                { "en", ["first child"] }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = true,
            CustomerId = CustomerId,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "second-child",
                    Parent = "FirstChildCollection",
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        });

        // Non-public child storage collection
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = "NonPublic",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", ["first child - private"] }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = false,
            CustomerId = CustomerId,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "non-public",
                    Parent = RootCollection.Id,
                    Type = ResourceType.StorageCollection,
                    Canonical = true
                }
            ]
        });

        // Child IIIF Collection
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = "IiifCollection",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", ["first child - iiif"] }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = false,
            IsPublic = true,
            CustomerId = CustomerId,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "iiif-collection",
                    Parent = RootCollection.Id,
                    Type = ResourceType.IIIFCollection,
                    Canonical = true
                }
            ]
        });
        
        // Child IIIF Collection with items
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = "IiifCollectionWithItems",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", ["first child - iiif"] }
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = false,
            IsPublic = true,
            CustomerId = CustomerId,
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "iiif-collection-with-items",
                    Parent = RootCollection.Id,
                    Type = ResourceType.IIIFCollection,
                    Canonical = true
                }
            ]
        });

        // Child manifest
        await DbContext.Manifests.AddAsync(new Manifest
        {
            Id = "FirstChildManifest",
            CustomerId = CustomerId,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "iiif-manifest",
                    Parent = RootCollection.Id,
                    Type = ResourceType.IIIFManifest,
                    Canonical = true
                }
            ],
            LastProcessed = DateTime.UtcNow
        });
        
        // processing child manifest
        await DbContext.Manifests.AddAsync(new Manifest
        {
            Id = "FirstChildManifestProcessing",
            CustomerId = CustomerId,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = "iiif-manifest-processing",
                    Parent = RootCollection.Id,
                    Type = ResourceType.IIIFManifest,
                    Canonical = true
                }
            ]
        });
        
        await DbContext.SaveChangesAsync();
    }
    
    public async Task InitializeAsync()
    {
        // Start DB + apply migrations
        try
        {
            await postgresContainer.StartAsync();
            SetPropertiesFromContainer();
            await DbContext.Database.MigrateAsync();
            await SeedCustomer();
        }
        catch (Exception ex)
        {
            _ = ex;
            throw;
        }
    }

    public async Task DisposeAsync() => await postgresContainer.StopAsync();
    
    private void SetPropertiesFromContainer()
    {
        ConnectionString = $"{postgresContainer.GetConnectionString()};Include Error Detail=true";

        // Create new PresentationContext using connection string for Postgres container
        DbContext = new PresentationContext(
            new DbContextOptionsBuilder<PresentationContext>()
                .UseNpgsql(ConnectionString, builder => builder.SetPostgresVersion(14, 0))
                .UseSnakeCaseNamingConvention()
                .Options
        );
        DbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public void CleanUp()
    {
        DbContext.Database.ExecuteSqlRaw(
            "DELETE FROM collections WHERE customer_id != 1 AND id NOT IN ('root','FirstChildCollection','SecondChildCollection', 'NonPublic', 'IiifCollection')");
        DbContext.Database.ExecuteSqlRaw(
            "DELETE FROM manifests WHERE customer_id != 1 AND id NOT IN ('FirstChildManifest', 'FirstChildManifestProcessing')");
    }
}
