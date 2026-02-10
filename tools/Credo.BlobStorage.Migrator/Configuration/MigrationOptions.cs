namespace Credo.BlobStorage.Migrator.Configuration;

/// <summary>
/// Configuration options for the migration process.
/// </summary>
public class MigrationOptions
{
    public const string SectionName = "Migration";

    /// <summary>
    /// SQL Server connection string for the source metadata database (main Documents DB).
    /// Contains MigrationLog table in migration schema.
    /// </summary>
    public string SourceConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server connection string for the content database (year-specific DB, e.g., Documents_2017).
    /// Contains DocumentsContent table with actual blob data.
    /// </summary>
    public string ContentConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the BlobStorage API.
    /// </summary>
    public string TargetApiBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Year being migrated (used for DocId generation).
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Name of the source content table in the content database (e.g., "DocumentsContent").
    /// </summary>
    public string ContentTable { get; set; } = "DocumentsContent";

    /// <summary>
    /// Target bucket name in the BlobStorage API.
    /// </summary>
    public string TargetBucket { get; set; } = "default";

    /// <summary>
    /// Number of documents to process per batch during migration.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of concurrent uploads.
    /// </summary>
    public int MaxParallelism { get; set; } = 4;

    /// <summary>
    /// Maximum retry attempts for failed uploads.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Worker token for parallel processing partitioning.
    /// When set, only processes records with matching WorkerToken value.
    /// When null, processes all records (no filtering).
    /// </summary>
    public int? WorkerToken { get; set; }
}
