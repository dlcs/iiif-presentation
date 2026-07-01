using System.Linq.Expressions;
using Core.Helpers;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Models.Database.Collections;
using Models.Database.General;
using Models.DLCS;
using Repository.Converters;
using Repository.Helpers;

namespace Repository;

public class PresentationContext : DbContext
{
    private readonly ICustomerIdProvider customerIdProvider;
    
    public PresentationContext(ICustomerIdProvider customerIdProvider)
    {
        this.customerIdProvider = customerIdProvider;
    }

    public PresentationContext(DbContextOptions<PresentationContext> options, ICustomerIdProvider customerIdProvider)
        : base(options)
    {
        this.customerIdProvider = customerIdProvider;
    }
    
    public int GetCurrentCustomerId() => customerIdProvider.GetCustomerId();

    public virtual DbSet<Collection> Collections { get; set; }
    
    public virtual DbSet<Hierarchy> Hierarchy { get; set; }
    
    public virtual DbSet<Manifest> Manifests { get; set; }

    public virtual DbSet<CanvasPainting> CanvasPaintings { get; set; }
    
    public virtual DbSet<Batch> Batches { get; set; }

    public virtual DbSet<PipelineJob> PipelineJobs { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<LanguageMap>()
            .HaveConversion<LanguageMapConverter, LanguageMapComparer>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        
        ApplyGlobalFilters(modelBuilder);

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.CustomerId });

            entity.Property(e => e.Label).HasColumnType("jsonb");

            entity.HasMany(e => e.Hierarchy)
                .WithOne(e => e.Collection)
                .HasForeignKey(e => new { e.CollectionId, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Children)
                .WithOne(e => e.ParentCollection)
                .HasForeignKey(e => new { e.Parent, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.PipelineJobs)
                .WithOne(e => e.Collection)
                .HasForeignKey(e => new { e.CollectionId, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Etag)
                .HasComputedColumnSql("""deterministic_uuid_sha256("modified", "id")""", stored: true);
        });

        modelBuilder.Entity<Manifest>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.CustomerId });

            entity.HasMany(e => e.Hierarchy)
                .WithOne(e => e.Manifest)
                .HasForeignKey(e => new { e.ManifestId, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.PipelineJobs)
                .WithOne(e => e.Manifest)
                .HasForeignKey(e => new { e.ManifestId, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.Created).HasDefaultValueSql("now()");
            entity.Property(p => p.Modified).HasDefaultValueSql("now()");
            
            entity.Property(e => e.Etag)
                .HasComputedColumnSql("""deterministic_uuid_sha256("last_processed", "id")""", stored: true);
        });

        modelBuilder.Entity<Hierarchy>(entity =>
        {
            // cannot have duplicate slugs with the same parent
            entity.HasIndex(e => new { e.CustomerId, e.Slug, e.Parent }).IsUnique();
            // only 1 canonical path is allowed per resource
            entity.HasIndex(e => new { e.ManifestId, e.CustomerId, e.Canonical })
                .IsUnique()
                .HasFilter("canonical is true");

            entity.ToTable(h => h.HasCheckConstraint("stop_collection_and_manifest_in_same_record",
                "num_nonnulls(manifest_id, collection_id) = 1"));

            entity.HasIndex(e => new { e.CollectionId, e.CustomerId, e.Canonical })
                .IsUnique()
                .HasFilter("canonical is true");

            entity.Ignore(p => p.ResourceId);
            entity.Ignore(p => p.FullPath);
            entity.Property(p => p.Slug).HasColumnType("citext");
        });

        modelBuilder.Entity<CanvasPainting>(entity =>
        {
            entity.HasKey(cp => cp.CanvasPaintingId);

            entity.Property(cp => cp.Id).HasColumnName("canvas_id");
            entity.Property(cp => cp.Label).HasColumnType("jsonb");
            entity.Property(p => p.Created).HasDefaultValueSql("now()");
            entity.Property(p => p.Modified).HasDefaultValueSql("now()");
            
            entity.HasIndex(cp => new
                    { cp.Id, cp.CustomerId, cp.ManifestId, cp.CanvasOrder, cp.ChoiceOrder })
                .IsUnique();

            entity.Property(cp => cp.AssetId)
                .HasConversion(id => id!.ToString(), id => AssetId.FromString(id));

            entity
                .HasOne(cp => cp.Manifest)
                .WithMany(m => m.CanvasPaintings)
                .HasForeignKey(cp => new { cp.ManifestId, cp.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Batch>(entity =>
        {
            entity.HasKey(b => new { b.Id, b.DeliverableType });

            entity
                .HasOne(cp => cp.Manifest)
                .WithMany(m => m.Batches)
                .HasForeignKey(cp => new { cp.ManifestId, cp.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion(
                    b => b.ToString(),
                    b => b.GetEnumFromString<BatchStatus>(true));

            entity.Property(e => e.DeliverableType)
                .IsRequired()
                .HasConversion(
                    d => d.ToString(),
                    d => d.GetEnumFromString<DeliverableType>(true));
        });

        modelBuilder.Entity<PipelineJob>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion(
                    s => s.ToString(),
                    s => s.GetEnumFromString<PipelineJobStatus>(true));

            entity.Property(e => e.JobType)
                .IsRequired()
                .HasConversion(
                    j => j.ToString(),
                    j => j.GetEnumFromString<PipelineJobType>(true));

            entity.Property(p => p.Created).HasDefaultValueSql("now()");

            entity.Property(e => e.Config)
                .HasConversion<PipelineConfigConverter>()
                .HasColumnType("jsonb");

            entity.Ignore(p => p.ResourceId);

            entity.ToTable(p => p.HasCheckConstraint("stop_collection_and_manifest_in_same_record",
                "num_nonnulls(manifest_id, collection_id) = 1"));
        });
    }

    private void ApplyGlobalFilters(ModelBuilder builder)
    {
        // get the method GetCustomerId from this class
        var currentCustomerIdMethod = typeof(PresentationContext).GetMethod(nameof(GetCurrentCustomerId))!;
        var methodCall = Expression.Call( Expression.Constant(this), currentCustomerIdMethod);
        
        // Automatically apply customer filter to all ICustomerEntity implementations
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ICustomerEntity).IsAssignableFrom(entityType.ClrType))
            {
                // grab the class from the entity
                var parameter = Expression.Parameter(entityType.ClrType, "entity");
                // grab the CustomerId property from the class
                var customerIdProperty = Expression.Property(parameter, nameof(ICustomerEntity.CustomerId));
                // create a lambda expression using the customer id property and the method call i.e.: entity.CustomerId == GetCustomerId()
                var filter = Expression.Lambda(
                    Expression.Equal(customerIdProperty, methodCall), 
                    parameter);
                // add the query filter to the entity
                builder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
