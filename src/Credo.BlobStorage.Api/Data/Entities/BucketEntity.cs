namespace Credo.BlobStorage.Api.Data.Entities;

/// <summary>
/// Represents a storage bucket in the database.
/// </summary>
public class BucketEntity
{
    /// <summary>
    /// Bucket name (primary key, S3-style naming).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// UTC timestamp when the bucket was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Navigation property to objects in this bucket.
    /// </summary>
    public ICollection<ObjectEntity> Objects { get; set; } = new List<ObjectEntity>();
}
