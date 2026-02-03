namespace Credo.BlobStorage.Migrator.Data.Source;

/// <summary>
/// Represents document content from the year-specific content database (e.g., Documents_2017).
/// Table: dbo.DocumentsContent
/// </summary>
public class SourceDocumentContent
{
    /// <summary>
    /// Primary key of the content record.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to Documents table in main database.
    /// </summary>
    public long? DocumentId { get; set; }

    /// <summary>
    /// Actual blob content.
    /// </summary>
    public byte[]? Documents { get; set; }

    /// <summary>
    /// Record date month.
    /// </summary>
    public int RecordDateMonth { get; set; }
}
