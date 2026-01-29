namespace Credo.BlobStorage.Api.Models.Responses;

/// <summary>
/// Response model for object information.
/// </summary>
public record ObjectResponse
{
    /// <summary>
    /// Unique document identifier in format: {yyyy}-{guid}
    /// </summary>
    public required string DocId { get; init; }

    /// <summary>
    /// Name of the bucket containing this object.
    /// </summary>
    public required string Bucket { get; init; }

    /// <summary>
    /// Original filename provided during upload.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// SHA-256 hash of the file content (hex string).
    /// </summary>
    public required string Sha256 { get; init; }

    /// <summary>
    /// Content type used when serving the file.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Content type detected from file magic bytes.
    /// </summary>
    public string? DetectedContentType { get; init; }

    /// <summary>
    /// File extension determined from detected content type.
    /// </summary>
    public string? DetectedExtension { get; init; }

    /// <summary>
    /// True if claimed content type differs from detected content type.
    /// </summary>
    public bool IsMismatch { get; init; }

    /// <summary>
    /// True if mismatch involves a potentially dangerous executable type.
    /// </summary>
    public bool IsDangerousMismatch { get; init; }

    /// <summary>
    /// UTC timestamp when the object was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// URL to download the file by DocId.
    /// </summary>
    public required string DownloadUrl { get; init; }

    /// <summary>
    /// URL to download the file by filename.
    /// </summary>
    public required string DownloadByNameUrl { get; init; }
}
