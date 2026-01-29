using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Data;
using Credo.BlobStorage.Api.Models.Responses;
using Credo.BlobStorage.Api.Services;
using Credo.BlobStorage.Core.Mime;
using Credo.BlobStorage.Core.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Credo.BlobStorage.Api.Controllers;

/// <summary>
/// Controller for object storage operations.
/// </summary>
[ApiController]
[Route("api/buckets/{bucket}/objects")]
[Produces("application/json")]
public class ObjectsController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly BlobStorageDbContext _context;
    private readonly StorageOptions _options;
    private readonly ILogger<ObjectsController> _logger;

    public ObjectsController(
        IStorageService storageService,
        BlobStorageDbContext context,
        IOptions<StorageOptions> options,
        ILogger<ObjectsController> logger)
    {
        _storageService = storageService;
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Lists objects in a bucket with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ObjectListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ObjectListResponse>> ListObjects(
        [FromRoute] string bucket,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? prefix = null,
        CancellationToken ct = default)
    {
        // Validate bucket exists
        var bucketExists = await _context.Buckets.AnyAsync(b => b.Name == bucket, ct);
        if (!bucketExists)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.BucketNotFound,
                    Message = $"Bucket '{bucket}' not found.",
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        // Clamp pagination values
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        var query = _context.Objects
            .Where(o => o.Bucket == bucket);

        if (!string.IsNullOrEmpty(prefix))
        {
            query = query.Where(o => o.Filename.StartsWith(prefix));
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var objects = await query
            .OrderBy(o => o.Filename)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new ObjectResponse
            {
                DocId = o.DocId,
                Bucket = o.Bucket,
                Filename = o.Filename,
                SizeBytes = o.SizeBytes,
                Sha256 = Convert.ToHexString(o.Sha256),
                ContentType = o.ServedContentType,
                DetectedContentType = o.DetectedContentType,
                DetectedExtension = o.DetectedExtension,
                IsMismatch = o.IsMismatch,
                IsDangerousMismatch = o.IsDangerousMismatch,
                CreatedAtUtc = o.CreatedAtUtc,
                DownloadUrl = $"/api/buckets/{o.Bucket}/objects/{o.DocId}",
                DownloadByNameUrl = $"/api/buckets/{o.Bucket}/objects/by-name/{Uri.EscapeDataString(o.Filename)}"
            })
            .ToListAsync(ct);

        return Ok(new ObjectListResponse
        {
            Objects = objects,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        });
    }

    /// <summary>
    /// Uploads a file via raw stream (filename in URL).
    /// </summary>
    [HttpPut("{*filename}")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(typeof(ObjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ObjectResponse>> Upload(
        [FromRoute] string bucket,
        [FromRoute] string filename,
        [FromQuery] int? year,
        [FromHeader(Name = "X-Claimed-Content-Type")] string? claimedContentType,
        CancellationToken ct)
    {
        // URL decode the filename
        filename = FilenameValidator.Normalize(filename);

        // Validate filename
        var filenameValidation = FilenameValidator.Validate(filename);
        if (!filenameValidation.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidFilename,
                    Message = filenameValidation.ErrorMessage!,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        try
        {
            var response = await _storageService.UploadAsync(
                bucket,
                filename,
                Request.Body,
                claimedContentType ?? Request.ContentType,
                year,
                ct);

            Response.Headers.ETag = response.Sha256;

            return CreatedAtAction(nameof(DownloadById), new { bucket, docId = response.DocId }, response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.BucketNotFound,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ObjectAlreadyExists,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidFilename,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
    }

    /// <summary>
    /// Uploads a file via multipart form (file picker).
    /// </summary>
    [HttpPost("form")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(typeof(ObjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ObjectResponse>> UploadForm(
        [FromRoute] string bucket,
        [FromQuery] int? year,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidFilename,
                    Message = "No file provided.",
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        var filename = file.FileName;

        // Validate filename
        var filenameValidation = FilenameValidator.Validate(filename);
        if (!filenameValidation.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidFilename,
                    Message = filenameValidation.ErrorMessage!,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await _storageService.UploadAsync(
                bucket,
                filename,
                stream,
                file.ContentType,
                year,
                ct);

            Response.Headers.ETag = response.Sha256;

            return CreatedAtAction(nameof(DownloadById), new { bucket, docId = response.DocId }, response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.BucketNotFound,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ObjectAlreadyExists,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidFilename,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
    }

    /// <summary>
    /// Downloads a file by DocId.
    /// </summary>
    [HttpGet("{docId}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadById(
        [FromRoute] string bucket,
        [FromRoute] string docId,
        CancellationToken ct)
    {
        try
        {
            var (content, metadata) = await _storageService.DownloadByIdAsync(bucket, docId, ct);
            return CreateFileResult(content, metadata);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ObjectNotFound,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "File not found on disk for DocId={DocId}", docId);
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.StorageError,
                    Message = "File not found on storage.",
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
    }

    /// <summary>
    /// Downloads a file by filename.
    /// </summary>
    [HttpGet("by-name/{*filename}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadByName(
        [FromRoute] string bucket,
        [FromRoute] string filename,
        CancellationToken ct)
    {
        filename = FilenameValidator.Normalize(filename);

        try
        {
            var (content, metadata) = await _storageService.DownloadByNameAsync(bucket, filename, ct);
            return CreateFileResult(content, metadata);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ObjectNotFound,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "File not found on disk for filename={Filename}", filename);
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.StorageError,
                    Message = "File not found on storage.",
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
    }

    /// <summary>
    /// Gets headers for a file by DocId (HEAD request).
    /// </summary>
    [HttpHead("{docId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HeadById(
        [FromRoute] string bucket,
        [FromRoute] string docId,
        CancellationToken ct)
    {
        var metadata = await _storageService.GetMetadataByIdAsync(bucket, docId, ct);
        if (metadata == null)
        {
            return NotFound();
        }

        SetResponseHeaders(metadata.ServedContentType, metadata.SizeBytes, metadata.Sha256,
            metadata.Filename, metadata.IsDangerousMismatch);

        return Ok();
    }

    /// <summary>
    /// Gets headers for a file by filename (HEAD request).
    /// </summary>
    [HttpHead("by-name/{*filename}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HeadByName(
        [FromRoute] string bucket,
        [FromRoute] string filename,
        CancellationToken ct)
    {
        filename = FilenameValidator.Normalize(filename);

        var metadata = await _storageService.GetMetadataByNameAsync(bucket, filename, ct);
        if (metadata == null)
        {
            return NotFound();
        }

        SetResponseHeaders(metadata.ServedContentType, metadata.SizeBytes, metadata.Sha256,
            metadata.Filename, metadata.IsDangerousMismatch);

        return Ok();
    }

    /// <summary>
    /// Deletes a file by DocId.
    /// </summary>
    [HttpDelete("{docId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteById(
        [FromRoute] string bucket,
        [FromRoute] string docId,
        CancellationToken ct)
    {
        try
        {
            await _storageService.DeleteByIdAsync(bucket, docId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ObjectNotFound,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
    }

    /// <summary>
    /// Deletes a file by filename.
    /// </summary>
    [HttpDelete("by-name/{*filename}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteByName(
        [FromRoute] string bucket,
        [FromRoute] string filename,
        CancellationToken ct)
    {
        filename = FilenameValidator.Normalize(filename);

        try
        {
            await _storageService.DeleteByNameAsync(bucket, filename, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ObjectNotFound,
                    Message = ex.Message,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }
    }

    private FileStreamResult CreateFileResult(Stream content, Data.Entities.ObjectEntity metadata)
    {
        SetResponseHeaders(metadata.ServedContentType, metadata.SizeBytes, metadata.Sha256,
            metadata.Filename, metadata.IsDangerousMismatch);

        return File(content, metadata.ServedContentType, metadata.Filename);
    }

    private void SetResponseHeaders(string contentType, long sizeBytes, byte[] sha256,
        string filename, bool isDangerousMismatch)
    {
        Response.Headers.ContentType = contentType;
        Response.Headers.ContentLength = sizeBytes;
        Response.Headers.ETag = Convert.ToHexString(sha256);

        // Determine content disposition
        ContentDispositionHeaderValue disposition;

        if (isDangerousMismatch)
        {
            // Force download, never inline
            disposition = new ContentDispositionHeaderValue("attachment");
        }
        else if (_options.InlineContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            disposition = new ContentDispositionHeaderValue("inline");
        }
        else
        {
            disposition = new ContentDispositionHeaderValue("attachment");
        }

        disposition.SetHttpFileName(filename);
        Response.Headers.ContentDisposition = disposition.ToString();
    }
}
