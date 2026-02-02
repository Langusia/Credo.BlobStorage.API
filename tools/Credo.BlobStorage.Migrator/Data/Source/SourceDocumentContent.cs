namespace Credo.BlobStorage.Migrator.Data.Source;

/// <summary>
/// Represents document content from the source SQL Server database.
/// </summary>
public class SourceDocumentContent
{
    /// <summary>
    /// Foreign key to Documents table.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Actual blob content.
    /// </summary>
    public byte[]? Documents { get; set; }
}
