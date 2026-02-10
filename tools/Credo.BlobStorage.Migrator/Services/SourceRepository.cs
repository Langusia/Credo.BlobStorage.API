using Credo.BlobStorage.Migrator.Data.Source;
using Microsoft.EntityFrameworkCore;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Repository for accessing source document content from SQL Server.
/// </summary>
public class SourceRepository : ISourceRepository
{
    private readonly ContentDbContext _contentContext;

    public SourceRepository(ContentDbContext contentContext)
    {
        _contentContext = contentContext;
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
}
