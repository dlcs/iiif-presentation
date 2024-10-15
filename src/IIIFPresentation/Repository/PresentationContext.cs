using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
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
    
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<LanguageMap>()
            .HaveConversion<LanguageMapConverter, LanguageMapComparer>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => new {e.Id, e.CustomerId});
            
            entity.Property(e => e.Label).HasColumnType("jsonb");
            
            // TODO: is there issues on deletions for hierarchy with manifest/collections with the same key?
            entity.HasMany(e => e.Hierarchy)
                .WithOne(e => e.Collection)
                .HasForeignKey(e => new { e.ResourceId, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Manifest>(entity =>
        {
            entity.HasKey(e => new {e.Id, e.CustomerId});
            
            entity.HasMany(e => e.Hierarchy)
                .WithOne(e => e.Manifest)
                .HasForeignKey(e => new { e.ResourceId, e.CustomerId })
                .HasPrincipalKey(e => new { e.Id, e.CustomerId })
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Hierarchy>(entity =>
        {
            // cannot have duplicate slugs with the same parent
            entity.HasIndex(e => new { e.CustomerId, e.Slug, e.Parent }).IsUnique();
            // only 1 canonical path is allowed per resource
            entity.HasIndex(e => new { e.ResourceId, e.CustomerId, e.Canonical, e.Type })
                .IsUnique()
                .HasFilter("canonical is true");
        });
    }
}