using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;
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
            entity.HasIndex(e => new { e.CustomerId, e.Slug, e.Parent }).IsUnique();
            
            entity.Property(e => e.Label).HasColumnType("jsonb");
        });
    }
}