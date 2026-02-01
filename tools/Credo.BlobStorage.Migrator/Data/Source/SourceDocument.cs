namespace Credo.BlobStorage.Migrator.Data.Source;

/// <summary>
/// Represents a document record from the source SQL Server database.
/// </summary>
public class SourceDocument
{
    /// <summary>
    /// Primary key - DocumentID.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Filename without extension.
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// Claimed file extension.
    /// </summary>
    public string? DocumentExt { get; set; }

    /// <summary>
    /// Claimed MIME content type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Record creation date.
    /// </summary>
    public DateTime RecordDate { get; set; }

    /// <summary>
    /// Soft delete flag - skip if true.
    /// </summary>
    public bool DelStatus { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public int FileSize { get; set; }

    /// <summary>
    /// Foreign key to content table (equals DocumentID).
    /// </summary>
    public long ContentId { get; set; }
}
