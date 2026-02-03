using Credo.BlobStorage.Migrator.Data.Source;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Repository for accessing source documents from SQL Server.
/// Uses two databases: main Documents DB for metadata and year-specific DB for content.
/// </summary>
public interface ISourceRepository
{
    /// <summary>
    /// Gets all DocumentIds from the content database that have content.
    /// This determines which documents can actually be migrated.
    /// </summary>
    Task<HashSet<long>> GetDocumentIdsWithContentAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets count of documents with content in the year-specific database.
    /// </summary>
    Task<int> GetContentCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets document metadata by ID from the main database.
    /// </summary>
    Task<SourceDocument?> GetDocumentAsync(long documentId, CancellationToken ct = default);

    /// <summary>
    /// Gets document metadata for multiple IDs from the main database (batch query).
    /// Only returns documents that are not deleted.
    /// </summary>
    Task<List<SourceDocument>> GetDocumentsForIdsAsync(IEnumerable<long> documentIds, CancellationToken ct = default);

    /// <summary>
    /// Gets document content by DocumentId from the content database.
    /// </summary>
    Task<byte[]?> GetDocumentContentAsync(long documentId, CancellationToken ct = default);

    /// <summary>
    /// Streams DocumentIds from the content database in batches.
    /// </summary>
    IAsyncEnumerable<long> StreamDocumentIdsWithContentAsync(CancellationToken ct = default);
}
