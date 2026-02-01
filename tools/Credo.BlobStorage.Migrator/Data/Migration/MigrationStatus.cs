namespace Credo.BlobStorage.Migrator.Data.Migration;

/// <summary>
/// Status of a migration log entry.
/// </summary>
public enum MigrationStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4
}
