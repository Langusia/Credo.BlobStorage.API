namespace Credo.BlobStorage.Api.Models.Responses;

/// <summary>
/// Standard error response model.
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// Error details.
    /// </summary>
    public required ErrorDetail Error { get; init; }
}

/// <summary>
/// Error detail information.
/// </summary>
public record ErrorDetail
{
    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Request identifier for tracking.
    /// </summary>
    public string? RequestId { get; init; }
}

/// <summary>
/// Predefined error codes.
/// </summary>
public static class ErrorCodes
{
    public const string BucketNotFound = "BucketNotFound";
    public const string BucketAlreadyExists = "BucketAlreadyExists";
    public const string BucketNotEmpty = "BucketNotEmpty";
    public const string InvalidBucketName = "InvalidBucketName";
    public const string ObjectNotFound = "ObjectNotFound";
    public const string ObjectAlreadyExists = "ObjectAlreadyExists";
    public const string InvalidFilename = "InvalidFilename";
    public const string FileTooLarge = "FileTooLarge";
    public const string InvalidContentType = "InvalidContentType";
    public const string StorageError = "StorageError";
    public const string InternalError = "InternalError";
}
