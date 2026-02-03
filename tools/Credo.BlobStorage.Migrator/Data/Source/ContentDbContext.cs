using Credo.BlobStorage.Migrator.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Migrator.Data.Source;

/// <summary>
/// Entity Framework context for the year-specific content database (e.g., Documents_2017).
/// Contains the DocumentsContent table with actual blob data.
/// </summary>
public class ContentDbContext : DbContext
{
    private readonly MigrationOptions _options;

    public ContentDbContext(
        DbContextOptions<ContentDbContext> options,
        IOptions<MigrationOptions> migrationOptions)
        : base(options)
    {
        _options = migrationOptions.Value;
    }

    public DbSet<SourceDocumentContent> DocumentContents => Set<SourceDocumentContent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure DocumentContent entity for year-specific content database
        modelBuilder.Entity<SourceDocumentContent>(entity =>
        {
            entity.ToTable(_options.ContentTable);
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id");

            entity.Property(e => e.DocumentId)
                .HasColumnName("DocumentId");

            entity.Property(e => e.Documents)
                .HasColumnType("varbinary(max)");

            entity.Property(e => e.RecordDateMonth)
                .HasColumnName("RecordDateMonth");
        });
    }
}
