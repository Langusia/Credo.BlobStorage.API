using Credo.BlobStorage.Migrator.Data.Source;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Repository for accessing source documents from SQL Server.
/// Uses two databases: main Documents DB for metadata and year-specific DB for content.
///
/// Binding: Documents.ContentId = DocumentsContent.Id
/// </summary>
public interface ISourceRepository
{
    /// <summary>
    /// Gets all Id values from DocumentsContent table (primary key).
    /// These IDs link to Documents.ContentId for metadata lookup.
    /// </summary>
    Task<HashSet<long>> GetContentIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets count of documents with content in the year-specific database.
    /// </summary>
    Task<int> GetContentCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets document metadata for multiple ContentIds from the main database.
    /// Queries Documents where ContentId IN (...).
    /// Only returns documents that are not deleted.
    /// </summary>
    Task<List<SourceDocument>> GetDocumentsForContentIdsAsync(IEnumerable<long> contentIds, CancellationToken ct = default);

    /// <summary>
    /// Gets document content by Id (DocumentsContent.Id primary key).
    /// </summary>
    Task<byte[]?> GetDocumentContentAsync(long contentId, CancellationToken ct = default);

    /// <summary>
    /// Streams Id values from DocumentsContent table.
    /// </summary>
    IAsyncEnumerable<long> StreamContentIdsAsync(CancellationToken ct = default);
}
