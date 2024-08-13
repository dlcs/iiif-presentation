﻿using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Repository;
using Testcontainers.PostgreSql;

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

        var stuff = DbContext.Model.GetEntityTypes();

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

        // Create new DlcsContext using connection string for Postgres container
        DbContext = new PresentationContext(
            new DbContextOptionsBuilder<PresentationContext>()
                .UseNpgsql(postgresContainer.GetConnectionString(), builder => builder.SetPostgresVersion(14, 0))
                .UseSnakeCaseNamingConvention()
                .Options
        );
        DbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }
}