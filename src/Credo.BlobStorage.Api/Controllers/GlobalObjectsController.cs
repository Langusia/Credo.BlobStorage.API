using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Data;
using Credo.BlobStorage.Api.Data.Entities;
using Credo.BlobStorage.Api.Models.Responses;
using Credo.BlobStorage.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Credo.BlobStorage.Api.Controllers;

/// <summary>
/// Controller for bucket-agnostic object operations using DocId.
/// </summary>
[ApiController]
[Route("api/objects")]
[Produces("application/json")]
public class GlobalObjectsController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly BlobStorageDbContext _context;
    private readonly StorageOptions _options;
    private readonly ILogger<GlobalObjectsController> _logger;

    public GlobalObjectsController(
        IStorageService storageService,
        BlobStorageDbContext context,
        IOptions<StorageOptions> options,
        ILogger<GlobalObjectsController> logger)
    {
        _storageService = storageService;
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Downloads a file by DocId (searches all buckets).
    /// </summary>
    [HttpGet("{docId}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadById(
        [FromRoute] string docId,
        CancellationToken ct)
    {
        var entity = await _context.Objects.FirstOrDefaultAsync(o => o.DocId == docId, ct);
        if (entity == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ObjectNotFound,
                    Message = $"Object with DocId '{docId}' not found.",
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        try
        {
            var (content, metadata) = await _storageService.DownloadByIdAsync(entity.Bucket, docId, ct);
            return CreateFileResult(content, metadata);
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
    /// Deletes a file by DocId (searches all buckets).
    /// </summary>
    [HttpDelete("{docId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteById(
        [FromRoute] string docId,
        CancellationToken ct)
    {
        var entity = await _context.Objects.FirstOrDefaultAsync(o => o.DocId == docId, ct);
        if (entity == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.ObjectNotFound,
                    Message = $"Object with DocId '{docId}' not found.",
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        try
        {
            await _storageService.DeleteByIdAsync(entity.Bucket, docId, ct);
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

    private FileStreamResult CreateFileResult(Stream content, ObjectEntity metadata)
    {
        Response.Headers.ContentType = metadata.ServedContentType;
        Response.Headers.ContentLength = metadata.SizeBytes;
        Response.Headers.ETag = Convert.ToHexString(metadata.Sha256);

        ContentDispositionHeaderValue disposition;

        if (metadata.IsDangerousMismatch)
        {
            disposition = new ContentDispositionHeaderValue("attachment");
        }
        else if (_options.InlineContentTypes.Contains(metadata.ServedContentType, StringComparer.OrdinalIgnoreCase))
        {
            disposition = new ContentDispositionHeaderValue("inline");
        }
        else
        {
            disposition = new ContentDispositionHeaderValue("attachment");
        }

        disposition.SetHttpFileName(metadata.Filename);
        Response.Headers.ContentDisposition = disposition.ToString();

        return File(content, metadata.ServedContentType, metadata.Filename);
    }
}
