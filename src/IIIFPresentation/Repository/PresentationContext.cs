using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
using Models.Database.General;
using Repository.Converters;
using Manifest = IIIF.Presentation.V2.Manifest;

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
    
    public virtual DbSet<Manifest> Manifests { get; set; }
    
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
            entity.HasIndex(e => new { e.CustomerId, e.Slug, e.Parent }).IsUnique();
            
            entity.Property(e => e.Label).HasColumnType("jsonb");
        });
        
        modelBuilder.Entity<Hierarchy>(entity =>
        {
            entity.ToTable(t =>
                t.HasCheckConstraint("opposite_must_be_null", "collection_id is null or manifest_id is null"));
            entity.HasIndex(e => new { e.Slug, e.Parent });
        });
    }
}