namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Repository for accessing source document content from SQL Server.
/// </summary>
public interface ISourceRepository
{
    /// <summary>
    /// Gets document content by Id (DocumentsContent.Id primary key).
    /// </summary>
    Task<byte[]?> GetDocumentContentAsync(long contentId, CancellationToken ct = default);
}
