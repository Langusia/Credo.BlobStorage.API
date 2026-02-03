namespace Credo.BlobStorage.Migrator.Data.Migration;

/// <summary>
/// Status of a migration log entry.
/// </summary>
public enum MigrationStatus
{
    /// <summary>
    /// Document ID seeded, awaiting metadata enrichment.
    /// </summary>
    Seeded = 0,

    /// <summary>
    /// Metadata enriched, ready for migration.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Migration in progress.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Successfully migrated.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Migration failed.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Skipped (no content or other reason).
    /// </summary>
    Skipped = 5
}
