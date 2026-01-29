using Credo.BlobStorage.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Api.Services;

/// <summary>
/// Builds filesystem paths for blob storage using year-based partitioning.
/// </summary>
public class PathBuilder : IPathBuilder
{
    private readonly StorageOptions _options;

    public PathBuilder(IOptions<StorageOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public string BuildBlobPath(string docId, string extension)
    {
        var directoryPath = BuildDirectoryPath(docId);
        var sanitizedExtension = extension.TrimStart('.');
        return Path.Combine(directoryPath, $"blob.{sanitizedExtension}");
    }

    /// <inheritdoc />
    public string BuildDirectoryPath(string docId)
    {
        var year = ExtractYear(docId);
        var guid = ExtractGuid(docId);

        // Get first 4 hex characters of the GUID for directory partitioning
        // Remove hyphens first, then take characters
        var guidNoHyphens = guid.Replace("-", "");
        var lvl1 = guidNoHyphens[..2].ToLowerInvariant();
        var lvl2 = guidNoHyphens[2..4].ToLowerInvariant();

        return Path.Combine(_options.RootPath, year.ToString(), lvl1, lvl2, docId);
    }

    /// <inheritdoc />
    public string BuildTempPath(string docId)
    {
        var directoryPath = BuildDirectoryPath(docId);
        return Path.Combine(directoryPath, "blob.tmp");
    }

    /// <inheritdoc />
    public string GenerateDocId(int? year = null)
    {
        var actualYear = year ?? DateTime.UtcNow.Year;
        var guid = Guid.NewGuid().ToString();
        return $"{actualYear}-{guid}";
    }

    /// <inheritdoc />
    public int ExtractYear(string docId)
    {
        if (string.IsNullOrEmpty(docId))
        {
            throw new ArgumentException("DocId cannot be null or empty.", nameof(docId));
        }

        var hyphenIndex = docId.IndexOf('-');
        if (hyphenIndex < 1)
        {
            throw new ArgumentException("Invalid DocId format. Expected format: yyyy-guid.", nameof(docId));
        }

        var yearStr = docId[..hyphenIndex];
        if (!int.TryParse(yearStr, out var year))
        {
            throw new ArgumentException("Invalid year in DocId.", nameof(docId));
        }

        return year;
    }

    /// <inheritdoc />
    public string ExtractGuid(string docId)
    {
        if (string.IsNullOrEmpty(docId))
        {
            throw new ArgumentException("DocId cannot be null or empty.", nameof(docId));
        }

        var hyphenIndex = docId.IndexOf('-');
        if (hyphenIndex < 1 || hyphenIndex >= docId.Length - 1)
        {
            throw new ArgumentException("Invalid DocId format. Expected format: yyyy-guid.", nameof(docId));
        }

        return docId[(hyphenIndex + 1)..];
    }
}
