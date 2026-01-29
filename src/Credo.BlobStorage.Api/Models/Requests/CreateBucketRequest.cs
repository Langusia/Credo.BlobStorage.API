using System.ComponentModel.DataAnnotations;

namespace Credo.BlobStorage.Api.Models.Requests;

/// <summary>
/// Request model for creating a new bucket.
/// </summary>
public record CreateBucketRequest
{
    /// <summary>
    /// Name of the bucket to create (S3-style naming rules apply).
    /// </summary>
    [Required]
    [StringLength(63, MinimumLength = 3)]
    public required string Name { get; init; }
}
