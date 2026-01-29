using Credo.BlobStorage.Api.Data.Entities;
using Credo.BlobStorage.Api.Models.Responses;

namespace Credo.BlobStorage.Api.Services;

/// <summary>
/// Interface for blob storage operations.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file to storage.
    /// </summary>
    /// <param name="bucket">Target bucket name.</param>
    /// <param name="filename">Filename for the object.</param>
    /// <param name="content">File content stream.</param>
    /// <param name="claimedContentType">Optional content type claimed by the client.</param>
    /// <param name="year">Optional year for DocId; uses current year if not provided.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Upload response with object metadata.</returns>
    Task<ObjectResponse> UploadAsync(
        string bucket,
        string filename,
        Stream content,
        string? claimedContentType = null,
        int? year = null,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads a file by DocId.
    /// </summary>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="docId">Document ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of file stream and metadata.</returns>
    Task<(Stream Content, ObjectEntity Metadata)> DownloadByIdAsync(
        string bucket,
        string docId,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads a file by filename.
    /// </summary>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="filename">Filename.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of file stream and metadata.</returns>
    Task<(Stream Content, ObjectEntity Metadata)> DownloadByNameAsync(
        string bucket,
        string filename,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a file by DocId.
    /// </summary>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="docId">Document ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteByIdAsync(string bucket, string docId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file by filename.
    /// </summary>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="filename">Filename.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteByNameAsync(string bucket, string filename, CancellationToken ct = default);

    /// <summary>
    /// Gets object metadata by DocId.
    /// </summary>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="docId">Document ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Object metadata or null if not found.</returns>
    Task<ObjectEntity?> GetMetadataByIdAsync(string bucket, string docId, CancellationToken ct = default);

    /// <summary>
    /// Gets object metadata by filename.
    /// </summary>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="filename">Filename.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Object metadata or null if not found.</returns>
    Task<ObjectEntity?> GetMetadataByNameAsync(string bucket, string filename, CancellationToken ct = default);
}
