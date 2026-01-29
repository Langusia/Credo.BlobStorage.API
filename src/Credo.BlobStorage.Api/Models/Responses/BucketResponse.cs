namespace Credo.BlobStorage.Api.Models.Responses;

/// <summary>
/// Response model for bucket information.
/// </summary>
public record BucketResponse
{
    /// <summary>
    /// Name of the bucket.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// UTC timestamp when the bucket was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Number of objects in the bucket.
    /// </summary>
    public long ObjectCount { get; init; }

    /// <summary>
    /// Total size of all objects in the bucket in bytes.
    /// </summary>
    public long TotalSizeBytes { get; init; }
}
