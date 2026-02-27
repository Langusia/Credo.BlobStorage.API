using Credo.BlobStorage.Api.Data;
using Credo.BlobStorage.Api.Data.Entities;
using Credo.BlobStorage.Api.Models.Requests;
using Credo.BlobStorage.Api.Models.Responses;
using Credo.BlobStorage.Core.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Credo.BlobStorage.Api.Controllers;

/// <summary>
/// Controller for bucket management operations.
/// </summary>
[ApiController]
[Route("api/buckets")]
[Produces("application/json")]
public class BucketsController : ControllerBase
{
    private readonly BlobStorageDbContext _context;
    private readonly ILogger<BucketsController> _logger;

    public BucketsController(BlobStorageDbContext context, ILogger<BucketsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lists all buckets.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BucketResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BucketResponse>>> ListBuckets(CancellationToken ct)
    {
        var buckets = await _context.Buckets
            .Select(b => new BucketResponse
            {
                Name = b.Name,
                CreatedAtUtc = b.CreatedAtUtc,
                ObjectCount = _context.Objects.Count(o => o.Bucket == b.Name),
                TotalSizeBytes = _context.Objects.Where(o => o.Bucket == b.Name).Sum(o => o.SizeBytes)
            })
            .ToListAsync(ct);

        return Ok(buckets);
    }

    /// <summary>
    /// Creates a new bucket.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BucketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BucketResponse>> CreateBucket(
        [FromBody] CreateBucketRequest request,
        CancellationToken ct)
    {
        // Validate bucket name
        var validation = BucketNameValidator.Validate(request.Name);
        if (!validation.IsValid)
        {
            _logger.LogWarning("CreateBucket failed: Invalid bucket name '{Name}' - {Error}",
                request.Name, validation.ErrorMessage);

            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidBucketName,
                    Message = validation.ErrorMessage!,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        // Check if bucket already exists
        var exists = await _context.Buckets.AnyAsync(b => b.Name == request.Name, ct);
        if (exists)
        {
            _logger.LogWarning("CreateBucket failed: Bucket '{Name}' already exists", request.Name);

            return Conflict(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.BucketAlreadyExists,
                    Message = $"Bucket '{request.Name}' already exists.",
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        var bucket = new BucketEntity
        {
            Name = request.Name,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Buckets.Add(bucket);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Bucket created: {Name}", request.Name);

        var response = new BucketResponse
        {
            Name = bucket.Name,
            CreatedAtUtc = bucket.CreatedAtUtc,
            ObjectCount = 0,
            TotalSizeBytes = 0
        };

        return CreatedAtAction(nameof(GetBucket), new { bucket = bucket.Name }, response);
    }

    /// <summary>
    /// Ensures a bucket exists. Creates it if missing, returns 200 if it already exists.
    /// </summary>
    [HttpPut("{bucket}")]
    [ProducesResponseType(typeof(BucketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BucketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BucketResponse>> EnsureBucket(
        [FromRoute] string bucket,
        CancellationToken ct)
    {
        var validation = BucketNameValidator.Validate(bucket);
        if (!validation.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.InvalidBucketName,
                    Message = validation.ErrorMessage!,
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        var entity = await _context.Buckets.FirstOrDefaultAsync(b => b.Name == bucket, ct);
        if (entity != null)
        {
            var objectCount = await _context.Objects.CountAsync(o => o.Bucket == bucket, ct);
            var totalSize = await _context.Objects
                .Where(o => o.Bucket == bucket)
                .SumAsync(o => o.SizeBytes, ct);

            return Ok(new BucketResponse
            {
                Name = entity.Name,
                CreatedAtUtc = entity.CreatedAtUtc,
                ObjectCount = objectCount,
                TotalSizeBytes = totalSize
            });
        }

        entity = new BucketEntity
        {
            Name = bucket,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Buckets.Add(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Bucket created via ensure: {Name}", bucket);

        var response = new BucketResponse
        {
            Name = entity.Name,
            CreatedAtUtc = entity.CreatedAtUtc,
            ObjectCount = 0,
            TotalSizeBytes = 0
        };

        return CreatedAtAction(nameof(GetBucket), new { bucket = entity.Name }, response);
    }

    /// <summary>
    /// Gets information about a specific bucket.
    /// </summary>
    [HttpGet("{bucket}")]
    [ProducesResponseType(typeof(BucketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BucketResponse>> GetBucket(
        [FromRoute] string bucket,
        CancellationToken ct)
    {
        var entity = await _context.Buckets.FirstOrDefaultAsync(b => b.Name == bucket, ct);
        if (entity == null)
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

        var objectCount = await _context.Objects.CountAsync(o => o.Bucket == bucket, ct);
        var totalSize = await _context.Objects
            .Where(o => o.Bucket == bucket)
            .SumAsync(o => o.SizeBytes, ct);

        return Ok(new BucketResponse
        {
            Name = entity.Name,
            CreatedAtUtc = entity.CreatedAtUtc,
            ObjectCount = objectCount,
            TotalSizeBytes = totalSize
        });
    }

    /// <summary>
    /// Deletes a bucket. The bucket must be empty.
    /// </summary>
    [HttpDelete("{bucket}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteBucket(
        [FromRoute] string bucket,
        CancellationToken ct)
    {
        var entity = await _context.Buckets.FirstOrDefaultAsync(b => b.Name == bucket, ct);
        if (entity == null)
        {
            _logger.LogWarning("DeleteBucket failed: Bucket '{Name}' not found", bucket);

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

        // Check if bucket is empty
        var hasObjects = await _context.Objects.AnyAsync(o => o.Bucket == bucket, ct);
        if (hasObjects)
        {
            _logger.LogWarning("DeleteBucket failed: Bucket '{Name}' is not empty", bucket);

            return Conflict(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = ErrorCodes.BucketNotEmpty,
                    Message = $"Bucket '{bucket}' is not empty. Delete all objects first.",
                    RequestId = HttpContext.TraceIdentifier
                }
            });
        }

        _context.Buckets.Remove(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Bucket deleted: {Name}", bucket);

        return NoContent();
    }
}
