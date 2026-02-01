namespace Credo.BlobStorage.Migrator.Data.Source;

/// <summary>
/// Represents document content from the source SQL Server database.
/// </summary>
public class SourceDocumentContent
{
    /// <summary>
    /// Primary key - matches DocumentID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Actual blob content.
    /// </summary>
    public byte[]? Documents { get; set; }
}
