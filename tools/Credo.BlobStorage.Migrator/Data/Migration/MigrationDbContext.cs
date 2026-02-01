using Microsoft.EntityFrameworkCore;

namespace Credo.BlobStorage.Migrator.Data.Migration;

/// <summary>
/// Entity Framework context for the migration log (PostgreSQL).
/// </summary>
public class MigrationDbContext : DbContext
{
    public MigrationDbContext(DbContextOptions<MigrationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MigrationLogEntry> MigrationLog => Set<MigrationLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MigrationLogEntry>(entity =>
        {
            entity.ToTable("MigrationLog");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn();

            entity.Property(e => e.SourceDocumentId)
                .IsRequired();

            entity.Property(e => e.SourceYear)
                .IsRequired();

            entity.Property(e => e.OriginalFilename)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.OriginalExtension)
                .HasMaxLength(10);

            entity.Property(e => e.ClaimedContentType)
                .HasMaxLength(50);

            entity.Property(e => e.SourceFileSize)
                .IsRequired();

            entity.Property(e => e.SourceRecordDate)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasDefaultValue(MigrationStatus.Pending);

            entity.Property(e => e.TargetDocId)
                .HasMaxLength(50);

            entity.Property(e => e.TargetBucket)
                .HasMaxLength(63);

            entity.Property(e => e.TargetFilename)
                .HasMaxLength(1024);

            entity.Property(e => e.TargetSha256)
                .HasMaxLength(64);

            entity.Property(e => e.DetectedContentType)
                .HasMaxLength(255);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            entity.Property(e => e.RetryCount)
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            entity.Property(e => e.ProcessedAtUtc);

            // Unique constraint on (SourceYear, SourceDocumentId)
            entity.HasIndex(e => new { e.SourceYear, e.SourceDocumentId })
                .IsUnique()
                .HasDatabaseName("UQ_MigrationLog_SourceYear_SourceDocumentId");

            // Index on Status for batch queries
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_MigrationLog_Status");
        });
    }
}
