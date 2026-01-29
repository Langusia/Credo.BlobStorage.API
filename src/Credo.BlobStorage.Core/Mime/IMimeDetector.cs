namespace Credo.BlobStorage.Core.Mime;

/// <summary>
/// Result of MIME type detection.
/// </summary>
/// <param name="DetectedContentType">The detected MIME type based on file content.</param>
/// <param name="DetectedExtension">The file extension corresponding to the detected type.</param>
/// <param name="DetectionMethod">How the MIME type was detected: "magic", "extension", "fallback".</param>
/// <param name="IsMismatch">True if claimed content type differs from detected.</param>
/// <param name="IsDangerousMismatch">True if mismatch involves a dangerous executable type.</param>
public record MimeDetectionResult(
    string DetectedContentType,
    string? DetectedExtension,
    string DetectionMethod,
    bool IsMismatch,
    bool IsDangerousMismatch
);

/// <summary>
/// Interface for detecting MIME types from file content.
/// </summary>
public interface IMimeDetector
{
    /// <summary>
    /// Detects MIME type from file header bytes and optional filename.
    /// </summary>
    /// <param name="headerBytes">First chunk of file data for magic byte detection.</param>
    /// <param name="filename">Optional filename for extension-based detection.</param>
    /// <param name="claimedContentType">Optional content type claimed by the client.</param>
    /// <returns>Detection result with type, extension, and mismatch information.</returns>
    MimeDetectionResult Detect(
        ReadOnlySpan<byte> headerBytes,
        string? filename = null,
        string? claimedContentType = null
    );
}
