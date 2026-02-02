using Credo.BlobStorage.Migrator.Data.Source;
using Microsoft.EntityFrameworkCore;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Repository for accessing source documents from SQL Server.
/// </summary>
public class SourceRepository : ISourceRepository
{
    private readonly SourceDbContext _context;

    public SourceRepository(SourceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<List<long>> GetAllDocumentIdsAsync(CancellationToken ct = default)
    {
        return await _context.Documents
            .Where(d => !d.DelStatus)
            .Select(d => d.DocumentId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SourceDocument?> GetDocumentAsync(long documentId, CancellationToken ct = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetDocumentContentAsync(long documentId, CancellationToken ct = default)
    {
        var content = await _context.DocumentContents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DocumentId == documentId, ct);

        return content?.Documents;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceDocument> GetDocumentsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var document in _context.Documents
            .AsNoTracking()
            .Where(d => !d.DelStatus)
            .OrderBy(d => d.DocumentId)
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            yield return document;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetDocumentCountAsync(CancellationToken ct = default)
    {
        return await _context.Documents
            .Where(d => !d.DelStatus)
            .CountAsync(ct);
    }
}
