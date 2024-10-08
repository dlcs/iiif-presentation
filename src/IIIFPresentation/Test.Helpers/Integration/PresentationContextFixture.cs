﻿using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Repository;
using Test.Helpers.Helpers;
using Testcontainers.PostgreSql;

#nullable disable

namespace Test.Helpers.Integration;

public class PresentationContextFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgresContainer;
    
    public PresentationContext DbContext { get; private set; }
    public string ConnectionString { get; private set; }
    
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
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = RootCollection.Id,
            Slug = "",
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

        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = "FirstChildCollection",
            Slug = "first-child",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", new List<string> {"first child"}}
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = true,
            CustomerId = 1,
            Parent = RootCollection.Id
        });
        
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = "SecondChildCollection",
            Slug = "second-child",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", new List<string> {"first child"}}
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = true,
            CustomerId = 1,
            Parent = "FirstChildCollection"
        });
        
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = "NonPublic",
            Slug = "non-public",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", new List<string> {"first child - private"}}
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = true,
            IsPublic = false,
            CustomerId = 1,
            Parent = RootCollection.Id
        });
        
        await DbContext.Collections.AddAsync(new Collection()
        {
            Id = "IiifCollection",
            Slug = "iiif-collection",
            UsePath = true,
            Label = new LanguageMap
            {
                {"en", new List<string> {"first child - iiif"}}
            },
            Thumbnail = "some/location",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            CreatedBy = "admin",
            Tags = "some, tags",
            IsStorageCollection = false,
            IsPublic = true,
            CustomerId = 1,
            Parent = RootCollection.Id
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
            var m = ex.Message;
            throw;
        }
    }

    public async Task DisposeAsync() => await postgresContainer.StopAsync();
    
    private void SetPropertiesFromContainer()
    {
        ConnectionString = postgresContainer.GetConnectionString();

        // Create new PresentationContext using connection string for Postgres container
        DbContext = new PresentationContext(
            new DbContextOptionsBuilder<PresentationContext>()
                .UseNpgsql(postgresContainer.GetConnectionString(), builder => builder.SetPostgresVersion(14, 0))
                .UseSnakeCaseNamingConvention()
                .Options
        );
        DbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public void CleanUp()
    {
        DbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM collections WHERE id NOT IN ('root','FirstChildCollection','SecondChildCollection', 'NonPublic', 'IiifCollection')");
    }
}