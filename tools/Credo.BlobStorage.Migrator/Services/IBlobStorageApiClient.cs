namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Result of an upload operation.
/// </summary>
public record UploadResult
{
    public bool Success { get; init; }
    public bool AlreadyExists { get; init; }
    public string? DocId { get; init; }
    public string? Sha256 { get; init; }
    public string? DetectedContentType { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Client for interacting with the BlobStorage API.
/// </summary>
public interface IBlobStorageApiClient
{
    /// <summary>
    /// Ensures the target bucket exists, creating it if necessary.
    /// </summary>
    Task<bool> EnsureBucketExistsAsync(string bucket, CancellationToken ct = default);

    /// <summary>
    /// Uploads a file to the BlobStorage API.
    /// </summary>
    Task<UploadResult> UploadAsync(
        string bucket,
        string filename,
        byte[] content,
        string? claimedContentType,
        int year,
        CancellationToken ct = default);
}
