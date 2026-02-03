using Credo.BlobStorage.Migrator.Data.Source;
using Microsoft.EntityFrameworkCore;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Repository for accessing source documents from SQL Server.
/// Uses two databases:
/// - SourceDbContext: Main Documents DB with metadata (Documents table)
/// - ContentDbContext: Year-specific DB with content (DocumentsContent table)
///
/// Binding: Documents.ContentId = DocumentsContent.Id
/// </summary>
public class SourceRepository : ISourceRepository
{
    private readonly SourceDbContext _metadataContext;
    private readonly ContentDbContext _contentContext;

    public SourceRepository(SourceDbContext metadataContext, ContentDbContext contentContext)
    {
        _metadataContext = metadataContext;
        _contentContext = contentContext;
    }

    /// <inheritdoc />
    public async Task<HashSet<long>> GetContentIdsAsync(CancellationToken ct = default)
    {
        // Get all Id values from content database (DocumentsContent.Id)
        // This is the primary key that links to Documents.ContentId
        var ids = await _contentContext.DocumentContents
            .AsNoTracking()
            .Where(c => c.Documents != null)
            .Select(c => c.Id)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    /// <inheritdoc />
    public async Task<int> GetContentCountAsync(CancellationToken ct = default)
    {
        return await _contentContext.DocumentContents
            .Where(c => c.Documents != null)
            .CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<SourceDocument>> GetDocumentsForContentIdsAsync(IEnumerable<long> contentIds, CancellationToken ct = default)
    {
        var idList = contentIds.ToList();

        // Query Documents where ContentId matches the DocumentsContent.Id values
        return await _metadataContext.Documents
            .AsNoTracking()
            .Where(d => idList.Contains(d.ContentId) && !d.DelStatus)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetDocumentContentAsync(long contentId, CancellationToken ct = default)
    {
        // Query by Id (primary key of DocumentsContent)
        var content = await _contentContext.DocumentContents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contentId, ct);

        return content?.Documents;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<long> StreamContentIdsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Stream Id values from content database (DocumentsContent.Id)
        await foreach (var id in _contentContext.DocumentContents
            .AsNoTracking()
            .Where(c => c.Documents != null)
            .Select(c => c.Id)
            .Distinct()
            .OrderBy(id => id)
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            yield return id;
        }
    }
}
