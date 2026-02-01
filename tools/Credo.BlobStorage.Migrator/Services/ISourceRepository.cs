using Credo.BlobStorage.Migrator.Data.Source;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Repository for accessing source documents from SQL Server.
/// </summary>
public interface ISourceRepository
{
    /// <summary>
    /// Gets all non-deleted document IDs from the source database.
    /// </summary>
    Task<List<long>> GetAllDocumentIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets document metadata by ID.
    /// </summary>
    Task<SourceDocument?> GetDocumentAsync(long documentId, CancellationToken ct = default);

    /// <summary>
    /// Gets document content by ID.
    /// </summary>
    Task<byte[]?> GetDocumentContentAsync(long documentId, CancellationToken ct = default);

    /// <summary>
    /// Gets documents in batches for seeding.
    /// </summary>
    IAsyncEnumerable<SourceDocument> GetDocumentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets count of non-deleted documents.
    /// </summary>
    Task<int> GetDocumentCountAsync(CancellationToken ct = default);
}
