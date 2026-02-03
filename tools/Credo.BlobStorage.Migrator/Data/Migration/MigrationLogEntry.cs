namespace Credo.BlobStorage.Migrator.Data.Migration;

/// <summary>
/// Represents a migration log entry tracking document migration status.
/// </summary>
public class MigrationLogEntry
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Source document ID from SQL Server.
    /// </summary>
    public long SourceDocumentId { get; set; }

    /// <summary>
    /// Source year (from table name).
    /// </summary>
    public int SourceYear { get; set; }

    /// <summary>
    /// Original filename (without extension). Null until metadata is enriched.
    /// </summary>
    public string? OriginalFilename { get; set; }

    /// <summary>
    /// Original claimed extension.
    /// </summary>
    public string? OriginalExtension { get; set; }

    /// <summary>
    /// Claimed content type from source.
    /// </summary>
    public string? ClaimedContentType { get; set; }

    /// <summary>
    /// File size from source. Null until metadata is enriched.
    /// </summary>
    public long? SourceFileSize { get; set; }

    /// <summary>
    /// Record date from source. Null until metadata is enriched.
    /// </summary>
    public DateTime? SourceRecordDate { get; set; }

    /// <summary>
    /// Current migration status.
    /// </summary>
    public MigrationStatus Status { get; set; } = MigrationStatus.Seeded;

    /// <summary>
    /// Target DocId after successful upload.
    /// </summary>
    public string? TargetDocId { get; set; }

    /// <summary>
    /// Target bucket name.
    /// </summary>
    public string? TargetBucket { get; set; }

    /// <summary>
    /// Target filename used in upload.
    /// </summary>
    public string? TargetFilename { get; set; }

    /// <summary>
    /// SHA-256 hash from target API response.
    /// </summary>
    public string? TargetSha256 { get; set; }

    /// <summary>
    /// Detected content type from target API.
    /// </summary>
    public string? DetectedContentType { get; set; }

    /// <summary>
    /// Error message if migration failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// When the log entry was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the migration was processed (completed/failed/skipped).
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }
}
