using System.Security.Cryptography;
using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Data;
using Credo.BlobStorage.Api.Data.Entities;
using Credo.BlobStorage.Api.Models.Responses;
using Credo.BlobStorage.Core.Mime;
using Credo.BlobStorage.Core.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Api.Services;

/// <summary>
/// Service for managing blob storage operations.
/// </summary>
public class StorageService : IStorageService
{
    private readonly BlobStorageDbContext _context;
    private readonly IPathBuilder _pathBuilder;
    private readonly IMimeDetector _mimeDetector;
    private readonly StorageOptions _options;
    private readonly ILogger<StorageService> _logger;

    public StorageService(
        BlobStorageDbContext context,
        IPathBuilder pathBuilder,
        IMimeDetector mimeDetector,
        IOptions<StorageOptions> options,
        ILogger<StorageService> logger)
    {
        _context = context;
        _pathBuilder = pathBuilder;
        _mimeDetector = mimeDetector;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ObjectResponse> UploadAsync(
        string bucket,
        string filename,
        Stream content,
        string? claimedContentType = null,
        int? year = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("UploadStarted: {Bucket}/{Filename}, ClaimedContentType={ClaimedContentType}",
            bucket, filename, claimedContentType);

        // Step 1-2: Validate bucket name
        var bucketValidation = BucketNameValidator.Validate(bucket);
        if (!bucketValidation.IsValid)
        {
            _logger.LogWarning("UploadFailed: {Bucket}/{Filename}, Stage=BucketValidation, Error={Error}",
                bucket, filename, bucketValidation.ErrorMessage);
            throw new InvalidOperationException(bucketValidation.ErrorMessage);
        }

        // Step 3: Validate filename
        var filenameValidation = FilenameValidator.Validate(filename);
        if (!filenameValidation.IsValid)
        {
            _logger.LogWarning("UploadFailed: {Bucket}/{Filename}, Stage=FilenameValidation, Error={Error}",
                bucket, filename, filenameValidation.ErrorMessage);
            throw new InvalidOperationException(filenameValidation.ErrorMessage);
        }

        // Step 1: Validate bucket exists
        var bucketExists = await _context.Buckets.AnyAsync(b => b.Name == bucket, ct);
        if (!bucketExists)
        {
            _logger.LogWarning("UploadFailed: {Bucket}/{Filename}, Stage=BucketNotFound",
                bucket, filename);
            throw new KeyNotFoundException($"Bucket '{bucket}' not found.");
        }

        // Step 4: Check filename uniqueness
        var filenameExists = await _context.Objects
            .AnyAsync(o => o.Bucket == bucket && o.Filename == filename, ct);
        if (filenameExists)
        {
            _logger.LogWarning("UploadConflict: {Bucket}/{Filename}", bucket, filename);
            throw new InvalidOperationException($"Object '{filename}' already exists in bucket '{bucket}'.");
        }

        // Step 5-6: Generate DocId
        var docId = _pathBuilder.GenerateDocId(year);
        var actualYear = _pathBuilder.ExtractYear(docId);

        string? tempPath = null;
        string? finalPath = null;

        try
        {
            // Step 7: Read first chunk for MIME detection
            var firstChunk = new byte[_options.FirstChunkSize];
            var firstChunkLength = await content.ReadAsync(firstChunk.AsMemory(0, _options.FirstChunkSize), ct);

            // Step 8: Detect MIME type
            var mimeResult = _mimeDetector.Detect(
                firstChunk.AsSpan(0, firstChunkLength),
                filename,
                claimedContentType);

            if (mimeResult.IsMismatch)
            {
                _logger.LogWarning("UploadMismatch: {Bucket}/{Filename}, Claimed={Claimed}, Detected={Detected}, IsDangerous={IsDangerous}",
                    bucket, filename, claimedContentType, mimeResult.DetectedContentType, mimeResult.IsDangerousMismatch);
            }

            // Determine extension
            var extension = mimeResult.DetectedExtension ?? MimeTypes.DefaultExtension;

            // Check if extension is allowed
            if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                extension = MimeTypes.DefaultExtension;
            }

            // Step 9-10: Build paths and create directories
            var directoryPath = _pathBuilder.BuildDirectoryPath(docId);
            Directory.CreateDirectory(directoryPath);

            tempPath = _pathBuilder.BuildTempPath(docId);
            finalPath = _pathBuilder.BuildBlobPath(docId, extension);

            // Step 11-13: Write file while computing hash
            long totalBytes;
            byte[] sha256Hash;

            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, _options.UploadBufferSize, useAsync: true))
            using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                // Write first chunk
                await fileStream.WriteAsync(firstChunk.AsMemory(0, firstChunkLength), ct);
                sha256.AppendData(firstChunk, 0, firstChunkLength);
                totalBytes = firstChunkLength;

                // Stream remaining content
                var buffer = new byte[_options.UploadBufferSize];
                int bytesRead;
                while ((bytesRead = await content.ReadAsync(buffer, ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    // Check size limit
                    if (totalBytes + bytesRead > _options.MaxUploadBytes)
                    {
                        throw new InvalidOperationException($"File exceeds maximum size of {_options.MaxUploadBytes} bytes.");
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    sha256.AppendData(buffer, 0, bytesRead);
                    totalBytes += bytesRead;
                }

                sha256Hash = sha256.GetHashAndReset();
            }

            // Step 14: Atomic rename
            File.Move(tempPath, finalPath);
            tempPath = null; // Clear so cleanup doesn't delete the final file

            // Step 15: Insert DB row
            var entity = new ObjectEntity
            {
                Bucket = bucket,
                Filename = filename,
                DocId = docId,
                Year = actualYear,
                SizeBytes = totalBytes,
                Sha256 = sha256Hash,
                ServedContentType = mimeResult.DetectedContentType,
                DetectedContentType = mimeResult.DetectedContentType,
                ClaimedContentType = claimedContentType,
                DetectedExtension = mimeResult.DetectedExtension,
                DetectionMethod = mimeResult.DetectionMethod,
                IsMismatch = mimeResult.IsMismatch,
                IsDangerousMismatch = mimeResult.IsDangerousMismatch,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Objects.Add(entity);
            await _context.SaveChangesAsync(ct);

            var durationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("UploadCompleted: {Bucket}/{Filename}, DocId={DocId}, SizeBytes={SizeBytes}, Sha256={Sha256}, DurationMs={DurationMs}",
                bucket, filename, docId, totalBytes, Convert.ToHexString(sha256Hash), durationMs);

            // Step 16: Return response
            return MapToResponse(entity);
        }
        catch (Exception ex) when (tempPath != null)
        {
            // Cleanup on failure after file was created
            _logger.LogError(ex, "UploadFailed: {Bucket}/{Filename}, Stage=FileWrite, Exception={Exception}",
                bucket, filename, ex.Message);

            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                // Also try to delete the directory if it was just created and is empty
                var directoryPath = _pathBuilder.BuildDirectoryPath(docId);
                if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
                {
                    Directory.Delete(directoryPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(Stream Content, ObjectEntity Metadata)> DownloadByIdAsync(
        string bucket,
        string docId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("DownloadStarted: {Bucket}/{DocId}", bucket, docId);
        var startTime = DateTime.UtcNow;

        var entity = await _context.Objects
            .FirstOrDefaultAsync(o => o.Bucket == bucket && o.DocId == docId, ct);

        if (entity == null)
        {
            _logger.LogWarning("DownloadNotFound: {Bucket}/{DocId}", bucket, docId);
            throw new KeyNotFoundException($"Object with DocId '{docId}' not found in bucket '{bucket}'.");
        }

        var extension = entity.DetectedExtension ?? MimeTypes.DefaultExtension;
        var filePath = _pathBuilder.BuildBlobPath(docId, extension);

        if (!File.Exists(filePath))
        {
            _logger.LogError("DownloadFailed: {Bucket}/{DocId}, File not found on disk at {Path}",
                bucket, docId, filePath);
            throw new FileNotFoundException($"File not found on disk for DocId '{docId}'.", filePath);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _options.UploadBufferSize, useAsync: true);

        var durationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("DownloadCompleted: {Bucket}/{DocId}, SizeBytes={SizeBytes}, DurationMs={DurationMs}",
            bucket, docId, entity.SizeBytes, durationMs);

        return (stream, entity);
    }

    /// <inheritdoc />
    public async Task<(Stream Content, ObjectEntity Metadata)> DownloadByNameAsync(
        string bucket,
        string filename,
        CancellationToken ct = default)
    {
        _logger.LogInformation("DownloadStarted: {Bucket}/by-name/{Filename}", bucket, filename);
        var startTime = DateTime.UtcNow;

        var entity = await _context.Objects
            .FirstOrDefaultAsync(o => o.Bucket == bucket && o.Filename == filename, ct);

        if (entity == null)
        {
            _logger.LogWarning("DownloadNotFound: {Bucket}/by-name/{Filename}", bucket, filename);
            throw new KeyNotFoundException($"Object '{filename}' not found in bucket '{bucket}'.");
        }

        var extension = entity.DetectedExtension ?? MimeTypes.DefaultExtension;
        var filePath = _pathBuilder.BuildBlobPath(entity.DocId, extension);

        if (!File.Exists(filePath))
        {
            _logger.LogError("DownloadFailed: {Bucket}/by-name/{Filename}, File not found on disk at {Path}",
                bucket, filename, filePath);
            throw new FileNotFoundException($"File not found on disk for '{filename}'.", filePath);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _options.UploadBufferSize, useAsync: true);

        var durationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("DownloadCompleted: {Bucket}/by-name/{Filename}, DocId={DocId}, SizeBytes={SizeBytes}, DurationMs={DurationMs}",
            bucket, filename, entity.DocId, entity.SizeBytes, durationMs);

        return (stream, entity);
    }

    /// <inheritdoc />
    public async Task DeleteByIdAsync(string bucket, string docId, CancellationToken ct = default)
    {
        var entity = await _context.Objects
            .FirstOrDefaultAsync(o => o.Bucket == bucket && o.DocId == docId, ct);

        if (entity == null)
        {
            _logger.LogWarning("DeleteNotFound: {Bucket}/{DocId}", bucket, docId);
            throw new KeyNotFoundException($"Object with DocId '{docId}' not found in bucket '{bucket}'.");
        }

        await DeleteEntityAsync(entity, ct);

        _logger.LogInformation("DeleteCompleted: {Bucket}/{DocId}, Filename={Filename}", bucket, docId, entity.Filename);
    }

    /// <inheritdoc />
    public async Task DeleteByNameAsync(string bucket, string filename, CancellationToken ct = default)
    {
        var entity = await _context.Objects
            .FirstOrDefaultAsync(o => o.Bucket == bucket && o.Filename == filename, ct);

        if (entity == null)
        {
            _logger.LogWarning("DeleteNotFound: {Bucket}/by-name/{Filename}", bucket, filename);
            throw new KeyNotFoundException($"Object '{filename}' not found in bucket '{bucket}'.");
        }

        await DeleteEntityAsync(entity, ct);

        _logger.LogInformation("DeleteCompleted: {Bucket}/{DocId}, Filename={Filename}", bucket, entity.DocId, filename);
    }

    private async Task DeleteEntityAsync(ObjectEntity entity, CancellationToken ct)
    {
        var extension = entity.DetectedExtension ?? MimeTypes.DefaultExtension;
        var filePath = _pathBuilder.BuildBlobPath(entity.DocId, extension);

        // Delete from database first
        _context.Objects.Remove(entity);
        await _context.SaveChangesAsync(ct);

        // Then delete from filesystem
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Try to clean up empty directories
            var directoryPath = _pathBuilder.BuildDirectoryPath(entity.DocId);
            if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteFailed: Could not delete file at {Path}", filePath);
            // Don't rethrow - DB deletion succeeded
        }
    }

    /// <inheritdoc />
    public async Task<ObjectEntity?> GetMetadataByIdAsync(string bucket, string docId, CancellationToken ct = default)
    {
        return await _context.Objects
            .FirstOrDefaultAsync(o => o.Bucket == bucket && o.DocId == docId, ct);
    }

    /// <inheritdoc />
    public async Task<ObjectEntity?> GetMetadataByNameAsync(string bucket, string filename, CancellationToken ct = default)
    {
        return await _context.Objects
            .FirstOrDefaultAsync(o => o.Bucket == bucket && o.Filename == filename, ct);
    }

    private ObjectResponse MapToResponse(ObjectEntity entity)
    {
        var encodedFilename = Uri.EscapeDataString(entity.Filename);

        return new ObjectResponse
        {
            DocId = entity.DocId,
            Bucket = entity.Bucket,
            Filename = entity.Filename,
            SizeBytes = entity.SizeBytes,
            Sha256 = Convert.ToHexString(entity.Sha256),
            ContentType = entity.ServedContentType,
            DetectedContentType = entity.DetectedContentType,
            DetectedExtension = entity.DetectedExtension,
            IsMismatch = entity.IsMismatch,
            IsDangerousMismatch = entity.IsDangerousMismatch,
            CreatedAtUtc = entity.CreatedAtUtc,
            DownloadUrl = $"/api/buckets/{entity.Bucket}/objects/{entity.DocId}",
            DownloadByNameUrl = $"/api/buckets/{entity.Bucket}/objects/by-name/{encodedFilename}"
        };
    }
}
