using Credo.BlobStorage.Client;
using Credo.BlobStorage.Client.Models;
using Microsoft.AspNetCore.Mvc;

namespace Credo.BlobStorage.TestApi.Controllers;

[ApiController]
[Route("api")]
public class BlobTestController : ControllerBase
{
    private readonly IBlobStorageClient _client;

    public BlobTestController(IBlobStorageClient client)
    {
        _client = client;
    }

    [HttpPut("upload/channel/{channel}/{operation}/{*filename}")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadByChannel(
        string channel, string operation, string filename, CancellationToken ct)
    {
        if (!Enum.TryParse<Channel>(channel, ignoreCase: true, out var parsedChannel))
        {
            var validValues = string.Join(", ", Enum.GetNames<Channel>());
            return BadRequest(new { errorCode = "InvalidChannel", errorMessage = $"Unknown channel '{channel}'. Valid values: {validValues}" });
        }

        var decodedFilename = Uri.UnescapeDataString(filename);
        var result = await _client.UploadAsync(parsedChannel, operation, decodedFilename, Request.Body, Request.ContentType, ct);

        return ToUploadResult(result);
    }

    [HttpPut("upload/bucket/{bucket}/{*filename}")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadByBucket(
        string bucket, string filename, CancellationToken ct)
    {
        var decodedFilename = Uri.UnescapeDataString(filename);
        var result = await _client.UploadAsync(bucket, decodedFilename, Request.Body, Request.ContentType, ct);

        return ToUploadResult(result);
    }

    [HttpGet("objects/{docId}")]
    public async Task<IActionResult> Get(string docId, CancellationToken ct)
    {
        var result = await _client.GetAsync(docId, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new { result.ErrorCode, result.ErrorMessage });

        return File(result.Value, "application/octet-stream");
    }

    [HttpDelete("objects/{docId}")]
    public async Task<IActionResult> Delete(string docId, CancellationToken ct)
    {
        var result = await _client.DeleteAsync(docId, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new { result.ErrorCode, result.ErrorMessage });

        return NoContent();
    }

    private IActionResult ToUploadResult(BlobStorageResult<UploadResponse> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return StatusCode(result.StatusCode, new { result.ErrorCode, result.ErrorMessage });
    }
}
