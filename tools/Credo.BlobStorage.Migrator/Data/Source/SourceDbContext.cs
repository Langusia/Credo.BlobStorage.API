using Credo.BlobStorage.Migrator.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Migrator.Data.Source;

/// <summary>
/// Entity Framework context for the source SQL Server database.
/// Uses dynamic table names from configuration.
/// </summary>
public class SourceDbContext : DbContext
{
    private readonly MigrationOptions _options;

    public SourceDbContext(
        DbContextOptions<SourceDbContext> options,
        IOptions<MigrationOptions> migrationOptions)
        : base(options)
    {
        _options = migrationOptions.Value;
    }

    public DbSet<SourceDocument> Documents => Set<SourceDocument>();
    public DbSet<SourceDocumentContent> DocumentContents => Set<SourceDocumentContent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Documents entity with dynamic table name
        modelBuilder.Entity<SourceDocument>(entity =>
        {
            entity.ToTable(_options.DocumentsTable);
            entity.HasKey(e => e.DocumentId);

            entity.Property(e => e.DocumentId)
                .HasColumnName("DocumentID");

            entity.Property(e => e.DocumentName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.DocumentExt)
                .HasMaxLength(10);

            entity.Property(e => e.ContentType)
                .HasMaxLength(50);

            entity.Property(e => e.RecordDate)
                .IsRequired();

            entity.Property(e => e.DelStatus)
                .IsRequired();

            entity.Property(e => e.FileSize)
                .IsRequired();

            entity.Property(e => e.ContentId)
                .HasColumnName("ContentId");
        });

        // Configure DocumentContent entity with dynamic table name
        modelBuilder.Entity<SourceDocumentContent>(entity =>
        {
            entity.ToTable(_options.ContentTable);
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id");

            entity.Property(e => e.Documents)
                .HasColumnType("varbinary(max)");
        });
    }
}
