using Credo.BlobStorage.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credo.BlobStorage.Api.Data;

/// <summary>
/// Entity Framework Core database context for blob storage.
/// </summary>
public class BlobStorageDbContext : DbContext
{
    public const string SchemaName = "blobStorage";

    public BlobStorageDbContext(DbContextOptions<BlobStorageDbContext> options)
        : base(options)
    {
    }

    public DbSet<BucketEntity> Buckets => Set<BucketEntity>();
    public DbSet<ObjectEntity> Objects => Set<ObjectEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set default schema
        modelBuilder.HasDefaultSchema(SchemaName);

        ConfigureBucketEntity(modelBuilder);
        ConfigureObjectEntity(modelBuilder);
    }

    private static void ConfigureBucketEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BucketEntity>(entity =>
        {
            entity.ToTable("Buckets");

            entity.HasKey(e => e.Name);

            entity.Property(e => e.Name)
                .HasMaxLength(63)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
        });
    }

    private static void ConfigureObjectEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ObjectEntity>(entity =>
        {
            entity.ToTable("Objects");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .UseIdentityColumn();

            entity.Property(e => e.Bucket)
                .HasMaxLength(63)
                .IsRequired();

            entity.Property(e => e.Filename)
                .HasMaxLength(1024)
                .IsRequired();

            entity.Property(e => e.DocId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Year)
                .IsRequired();

            entity.Property(e => e.SizeBytes)
                .IsRequired();

            entity.Property(e => e.Sha256)
                .HasColumnType("varbinary(32)")
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(e => e.ServedContentType)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.DetectedContentType)
                .HasMaxLength(255);

            entity.Property(e => e.ClaimedContentType)
                .HasMaxLength(255);

            entity.Property(e => e.DetectedExtension)
                .HasMaxLength(20);

            entity.Property(e => e.DetectionMethod)
                .HasMaxLength(50);

            entity.Property(e => e.IsMismatch)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(e => e.IsDangerousMismatch)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            // Unique constraint on DocId
            entity.HasIndex(e => e.DocId)
                .IsUnique()
                .HasDatabaseName("IX_Objects_DocId");

            // Index on Bucket for faster lookups
            entity.HasIndex(e => e.Bucket)
                .HasDatabaseName("IX_Objects_Bucket");

            // Unique constraint on Bucket + Filename combination
            entity.HasIndex(e => new { e.Bucket, e.Filename })
                .IsUnique()
                .HasDatabaseName("UQ_Objects_Bucket_Filename");

            // Foreign key relationship to Buckets
            entity.HasOne(e => e.BucketNavigation)
                .WithMany(b => b.Objects)
                .HasForeignKey(e => e.Bucket)
                .HasPrincipalKey(b => b.Name)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
