using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Credo.BlobStorage.Migrator.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Client for interacting with the BlobStorage API.
/// </summary>
public class BlobStorageApiClient : IBlobStorageApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlobStorageApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public BlobStorageApiClient(
        HttpClient httpClient,
        ILogger<BlobStorageApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<bool> EnsureBucketExistsAsync(string bucket, CancellationToken ct = default)
    {
        try
        {
            // Check if bucket exists
            var response = await _httpClient.GetAsync($"/api/buckets/{bucket}", ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Bucket '{Bucket}' already exists", bucket);
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Create bucket
                _logger.LogInformation("Creating bucket '{Bucket}'", bucket);
                var createResponse = await _httpClient.PostAsJsonAsync(
                    "/api/buckets",
                    new { name = bucket },
                    ct);

                if (createResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Bucket '{Bucket}' created successfully", bucket);
                    return true;
                }

                var error = await createResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to create bucket '{Bucket}': {Error}", bucket, error);
                return false;
            }

            _logger.LogError("Unexpected response checking bucket '{Bucket}': {StatusCode}",
                bucket, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring bucket '{Bucket}' exists", bucket);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<UploadResult> UploadAsync(
        string bucket,
        string filename,
        byte[] content,
        string? claimedContentType,
        int year,
        CancellationToken ct = default)
    {
        try
        {
            var encodedFilename = Uri.EscapeDataString(filename);
            var url = $"/api/buckets/{bucket}/objects/{encodedFilename}?year={year}";

            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Content = new ByteArrayContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            if (!string.IsNullOrEmpty(claimedContentType))
            {
                request.Headers.TryAddWithoutValidation("X-Claimed-Content-Type", claimedContentType);
            }

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                var uploadResponse = JsonSerializer.Deserialize<UploadApiResponse>(responseBody, _jsonOptions);

                return new UploadResult
                {
                    Success = true,
                    DocId = uploadResponse?.DocId,
                    Sha256 = uploadResponse?.Sha256,
                    DetectedContentType = uploadResponse?.DetectedContentType
                };
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new UploadResult
                {
                    Success = true,
                    AlreadyExists = true
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(ct);
            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"HTTP {(int)response.StatusCode}: {errorContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for {Filename}", filename);
            return new UploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private class UploadApiResponse
    {
        public string? DocId { get; set; }
        public string? Sha256 { get; set; }
        public string? DetectedContentType { get; set; }
    }
}
