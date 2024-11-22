using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Models.Database.Collections;
using Models.Database.General;
using Repository.Converters;

namespace Repository;

public class PresentationContext : DbContext
{
    public PresentationContext()
    {
    }

    public PresentationContext(DbContextOptions<PresentationContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Collection> Collections { get; set; }
    
    public virtual DbSet<Hierarchy> Hierarchy { get; set; }
    
    public virtual DbSet<Manifest> Manifests { get; set; }

    public virtual DbSet<CanvasPainting> CanvasPaintings { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<LanguageMap>()
            .HaveConversion<LanguageMapConverter, LanguageMapComparer>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.CustomerId });
            
            entity.Property(e => e.Label).HasColumnType("jsonb");
            
            entity.HasMany(e => e.Hierarchy)
                .WithOne(e => e.Collection)
                .HasForeignKey(e => new { e.CollectionId, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Manifest>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.CustomerId });
            
            entity.HasMany(e => e.Hierarchy)
                .WithOne(e => e.Manifest)
                .HasForeignKey(e => new { e.ManifestId, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.Created).HasDefaultValueSql("now()");
            entity.Property(p => p.Modified).HasDefaultValueSql("now()");
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
            entity.Property(cp => cp.Label).HasColumnType("jsonb");
            entity.Property(p => p.Created).HasDefaultValueSql("now()");
            entity.Property(p => p.Modified).HasDefaultValueSql("now()");
            
            entity
                .HasOne(cp => cp.Manifest)
                .WithMany(m => m.CanvasPaintings)
                .HasForeignKey(cp => new { cp.ManifestId, cp.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}