using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credo.BlobStorage.Client.Models;

namespace Credo.BlobStorage.Client
{
    public interface IBlobStorageClient
    {
        /// <summary>
        /// Uploads a file using channel + operation as bucket name ({channel}-{operation}).
        /// </summary>
        Task<BlobStorageResult<UploadResponse>> UploadAsync(
            Channel channel, string operation, string filename,
            Stream content, string? contentType = null, CancellationToken ct = default);

        /// <summary>
        /// Uploads a byte array using channel + operation as bucket name ({channel}-{operation}).
        /// </summary>
        Task<BlobStorageResult<UploadResponse>> UploadAsync(
            Channel channel, string operation, string filename,
            byte[] content, string? contentType = null, CancellationToken ct = default);

        /// <summary>
        /// Uploads a file to the specified bucket directly.
        /// </summary>
        Task<BlobStorageResult<UploadResponse>> UploadAsync(
            string bucket, string filename,
            Stream content, string? contentType = null, CancellationToken ct = default);

        /// <summary>
        /// Uploads a byte array to the specified bucket directly.
        /// </summary>
        Task<BlobStorageResult<UploadResponse>> UploadAsync(
            string bucket, string filename,
            byte[] content, string? contentType = null, CancellationToken ct = default);

        /// <summary>
        /// Downloads a file by DocId (searches all buckets).
        /// </summary>
        Task<BlobStorageResult<Stream>> GetAsync(string docId, CancellationToken ct = default);

        /// <summary>
        /// Deletes a file by DocId (searches all buckets).
        /// </summary>
        Task<BlobStorageResult> DeleteAsync(string docId, CancellationToken ct = default);
    }
}
