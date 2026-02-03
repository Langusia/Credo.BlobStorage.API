using Credo.BlobStorage.Migrator.Data.Source;
using Microsoft.EntityFrameworkCore;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Repository for accessing source documents from SQL Server.
/// Uses two databases:
/// - SourceDbContext: Main Documents DB with metadata (Documents_{Year} tables)
/// - ContentDbContext: Year-specific DB with content (DocumentsContent table)
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
    public async Task<HashSet<long>> GetDocumentIdsWithContentAsync(CancellationToken ct = default)
    {
        // Get all DocumentIds from content database that have content
        var ids = await _contentContext.DocumentContents
            .AsNoTracking()
            .Where(c => c.DocumentId != null && c.Documents != null)
            .Select(c => c.DocumentId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    /// <inheritdoc />
    public async Task<int> GetContentCountAsync(CancellationToken ct = default)
    {
        return await _contentContext.DocumentContents
            .Where(c => c.DocumentId != null && c.Documents != null)
            .CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SourceDocument?> GetDocumentAsync(long documentId, CancellationToken ct = default)
    {
        return await _metadataContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
    }

    /// <inheritdoc />
    public async Task<List<SourceDocument>> GetDocumentsForIdsAsync(IEnumerable<long> documentIds, CancellationToken ct = default)
    {
        var idList = documentIds.ToList();

        return await _metadataContext.Documents
            .AsNoTracking()
            .Where(d => idList.Contains(d.DocumentId) && !d.DelStatus)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetDocumentContentAsync(long documentId, CancellationToken ct = default)
    {
        var content = await _contentContext.DocumentContents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DocumentId == documentId, ct);

        return content?.Documents;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<long> StreamDocumentIdsWithContentAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Stream DocumentIds from content database
        await foreach (var documentId in _contentContext.DocumentContents
            .AsNoTracking()
            .Where(c => c.DocumentId != null && c.Documents != null)
            .Select(c => c.DocumentId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            yield return documentId;
        }
    }
}
