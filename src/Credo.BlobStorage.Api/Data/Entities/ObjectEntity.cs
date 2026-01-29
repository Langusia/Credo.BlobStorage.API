namespace Credo.BlobStorage.Api.Data.Entities;

/// <summary>
/// Represents a stored object in the database.
/// </summary>
public class ObjectEntity
{
    /// <summary>
    /// Auto-incrementing primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name of the bucket containing this object.
    /// </summary>
    public required string Bucket { get; set; }

    /// <summary>
    /// Original filename provided by the client.
    /// </summary>
    public required string Filename { get; set; }

    /// <summary>
    /// Unique document identifier in format: {yyyy}-{guid}
    /// </summary>
    public required string DocId { get; set; }

    /// <summary>
    /// Year portion of the DocId, used for filesystem partitioning.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// SHA-256 hash of the file content (32 bytes).
    /// </summary>
    public required byte[] Sha256 { get; set; }

    /// <summary>
    /// Content type used when serving the file.
    /// </summary>
    public required string ServedContentType { get; set; }

    /// <summary>
    /// Content type detected from file magic bytes.
    /// </summary>
    public string? DetectedContentType { get; set; }

    /// <summary>
    /// Content type claimed by the client during upload.
    /// </summary>
    public string? ClaimedContentType { get; set; }

    /// <summary>
    /// File extension determined from detected content type.
    /// </summary>
    public string? DetectedExtension { get; set; }

    /// <summary>
    /// Method used to detect the MIME type: "magic", "extension", "fallback".
    /// </summary>
    public string? DetectionMethod { get; set; }

    /// <summary>
    /// True if claimed content type differs from detected content type.
    /// </summary>
    public bool IsMismatch { get; set; }

    /// <summary>
    /// True if mismatch involves a potentially dangerous executable type.
    /// </summary>
    public bool IsDangerousMismatch { get; set; }

    /// <summary>
    /// UTC timestamp when the object was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Navigation property to the parent bucket.
    /// </summary>
    public BucketEntity? BucketNavigation { get; set; }
}
