using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credo.BlobStorage.Client.Models;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Client
{
    public class BlobStorageClient : IBlobStorageClient
    {
        private readonly HttpClient _httpClient;
        private readonly BlobStorageClientOptions _options;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public BlobStorageClient(HttpClient httpClient, IOptions<BlobStorageClientOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public Task<BlobStorageResult<UploadResponse>> UploadAsync(
            Channel channel, string operation, string filename,
            Stream content, string? contentType = null, CancellationToken ct = default)
        {
            var bucket = $"{channel.ToBucketPrefix()}-{operation}";
            return UploadCoreAsync(bucket, filename, content, contentType, ct);
        }

        public async Task<BlobStorageResult<UploadResponse>> UploadAsync(
            Channel channel, string operation, string filename,
            byte[] content, string? contentType = null, CancellationToken ct = default)
        {
            using (var stream = new MemoryStream(content))
            {
                return await UploadCoreAsync(
                    $"{channel.ToBucketPrefix()}-{operation}",
                    filename, stream, contentType, ct).ConfigureAwait(false);
            }
        }

        public Task<BlobStorageResult<UploadResponse>> UploadAsync(
            string bucket, string filename,
            Stream content, string? contentType = null, CancellationToken ct = default)
        {
            return UploadCoreAsync(bucket, filename, content, contentType, ct);
        }

        public async Task<BlobStorageResult<UploadResponse>> UploadAsync(
            string bucket, string filename,
            byte[] content, string? contentType = null, CancellationToken ct = default)
        {
            using (var stream = new MemoryStream(content))
            {
                return await UploadCoreAsync(bucket, filename, stream, contentType, ct).ConfigureAwait(false);
            }
        }

        public async Task<BlobStorageResult<Stream>> GetAsync(string docId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync(
                $"api/objects/{Uri.EscapeDataString(docId)}",
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return BlobStorageResult<Stream>.Success((int)response.StatusCode, stream);
            }

            return await CreateFailureResultAsync<Stream>(response).ConfigureAwait(false);
        }

        public async Task<BlobStorageResult> DeleteAsync(string docId, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync(
                $"api/objects/{Uri.EscapeDataString(docId)}", ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return BlobStorageResult.Success((int)response.StatusCode);
            }

            return await CreateFailureResultAsync(response).ConfigureAwait(false);
        }

        private async Task<BlobStorageResult<UploadResponse>> UploadCoreAsync(
            string bucket, string filename, Stream content, string? contentType, CancellationToken ct)
        {
            if (_options.AutoCreateBuckets)
            {
                await EnsureBucketExistsAsync(bucket, ct).ConfigureAwait(false);
            }

            var encodedFilename = EncodeFilename(filename);
            var requestUri = $"api/buckets/{Uri.EscapeDataString(bucket)}/objects/{encodedFilename}";

            using (var request = new HttpRequestMessage(HttpMethod.Put, requestUri))
            {
                request.Content = new StreamContent(content);
                if (contentType != null)
                {
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(body, JsonOptions)!;
                    return BlobStorageResult<UploadResponse>.Success((int)response.StatusCode, uploadResponse);
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    // 409 = object already exists — treat as success
                    return BlobStorageResult<UploadResponse>.Success((int)response.StatusCode, null!);
                }

                return await CreateFailureResultAsync<UploadResponse>(response).ConfigureAwait(false);
            }
        }

        private async Task EnsureBucketExistsAsync(string bucket, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(new { name = bucket }, JsonOptions);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                using (var response = await _httpClient.PostAsync("api/buckets", content, ct).ConfigureAwait(false))
                {
                    // 201 = created, 409 = already exists — both are fine
                }
            }
        }

        private static async Task<BlobStorageResult> CreateFailureResultAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            try
            {
                var error = JsonSerializer.Deserialize<ErrorResponseDto>(body, JsonOptions);
                return BlobStorageResult.Failure((int)response.StatusCode, error?.Error?.Code, error?.Error?.Message);
            }
            catch
            {
                return BlobStorageResult.Failure((int)response.StatusCode, null, body);
            }
        }

        private static async Task<BlobStorageResult<T>> CreateFailureResultAsync<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            try
            {
                var error = JsonSerializer.Deserialize<ErrorResponseDto>(body, JsonOptions);
                return BlobStorageResult<T>.Failure((int)response.StatusCode, error?.Error?.Code, error?.Error?.Message);
            }
            catch
            {
                return BlobStorageResult<T>.Failure((int)response.StatusCode, null, body);
            }
        }

        /// <summary>
        /// Encodes each path segment of the filename, preserving '/' separators.
        /// </summary>
        private static string EncodeFilename(string filename)
        {
            var segments = filename.Split('/');
            return string.Join("/", segments.Select(Uri.EscapeDataString));
        }

        private class ErrorResponseDto
        {
            public ErrorDetailDto? Error { get; set; }
        }

        private class ErrorDetailDto
        {
            public string? Code { get; set; }
            public string? Message { get; set; }
        }
    }
}
