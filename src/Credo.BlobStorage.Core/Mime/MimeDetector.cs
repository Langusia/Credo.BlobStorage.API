using System.IO.Compression;
using System.Text;

namespace Credo.BlobStorage.Core.Mime;

/// <summary>
/// Multi-layer MIME type detector using magic bytes, ZIP inspection, and extension fallback.
/// </summary>
public class MimeDetector : IMimeDetector
{
    private const double TextHeuristicThreshold = 0.85;

    /// <inheritdoc />
    public MimeDetectionResult Detect(
        ReadOnlySpan<byte> headerBytes,
        string? filename = null,
        string? claimedContentType = null)
    {
        string detectedContentType;
        string? detectedExtension;
        string detectionMethod;

        // Step 1: Try magic bytes detection
        var magicResult = DetectByMagicBytes(headerBytes);
        if (magicResult.HasValue)
        {
            detectedContentType = magicResult.Value.MimeType;
            detectedExtension = magicResult.Value.Extension;
            detectionMethod = "magic";

            // Step 2: If ZIP, try to identify OOXML Office documents
            if (detectedContentType == "application/zip" && headerBytes.Length >= 30)
            {
                var ooxmlResult = TryDetectOoxml(headerBytes);
                if (ooxmlResult.HasValue)
                {
                    detectedContentType = ooxmlResult.Value.MimeType;
                    detectedExtension = ooxmlResult.Value.Extension;
                }
            }

            // Step 3: If OLE2, check extension for specific Office type
            if (detectedContentType == "application/msword" && !string.IsNullOrEmpty(filename))
            {
                var ole2Result = TryDetectOle2ByExtension(filename);
                if (ole2Result.HasValue)
                {
                    detectedContentType = ole2Result.Value.MimeType;
                    detectedExtension = ole2Result.Value.Extension;
                }
            }

            return CreateResult(detectedContentType, detectedExtension, detectionMethod, claimedContentType);
        }

        // Step 4: Try RIFF-based detection (WebP, WAV, AVI)
        var riffResult = TryDetectRiff(headerBytes);
        if (riffResult.HasValue)
        {
            return CreateResult(riffResult.Value.MimeType, riffResult.Value.Extension, "magic", claimedContentType);
        }

        // Step 5: Use claimed content type if provided and valid
        if (!string.IsNullOrEmpty(claimedContentType) &&
            MimeTypes.MimeToExtension.TryGetValue(claimedContentType, out var claimedExt))
        {
            return CreateResult(claimedContentType, claimedExt, "header", claimedContentType);
        }

        // Step 6: Fall back to file extension
        if (!string.IsNullOrEmpty(filename))
        {
            var ext = Path.GetExtension(filename);
            if (!string.IsNullOrEmpty(ext) && MimeTypes.ExtensionMap.TryGetValue(ext, out var extMimeType))
            {
                var extOnly = ext.TrimStart('.');
                return CreateResult(extMimeType, extOnly, "extension", claimedContentType);
            }
        }

        // Step 7: Text heuristic
        if (IsLikelyText(headerBytes))
        {
            return CreateResult("text/plain", "txt", "heuristic", claimedContentType);
        }

        // Step 8: Default fallback
        return CreateResult(MimeTypes.DefaultMimeType, MimeTypes.DefaultExtension, "fallback", claimedContentType);
    }

    private static (string MimeType, string Extension)? DetectByMagicBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
            return null;

        var hexString = Convert.ToHexString(data.Length >= 8 ? data[..8] : data);

        // Check longest signatures first
        foreach (var (signature, result) in MimeTypes.MagicBytes.OrderByDescending(x => x.Key.Length))
        {
            if (hexString.StartsWith(signature, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }

        return null;
    }

    private static (string MimeType, string Extension)? TryDetectOoxml(ReadOnlySpan<byte> data)
    {
        // ZIP files start with PK, check for OOXML markers
        try
        {
            // Look for OOXML content in the ZIP central directory
            var dataArray = data.ToArray();
            using var stream = new MemoryStream(dataArray);

            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

                foreach (var entry in archive.Entries)
                {
                    foreach (var (marker, result) in MimeTypes.OoxmlTypes)
                    {
                        if (entry.FullName.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                        {
                            return result;
                        }
                    }

                    // Check for [Content_Types].xml which is present in all OOXML
                    if (entry.FullName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                    {
                        // Continue checking for specific type
                        continue;
                    }
                }
            }
            catch
            {
                // Not a valid ZIP or incomplete data
            }
        }
        catch
        {
            // Ignore ZIP parsing errors
        }

        return null;
    }

    private static (string MimeType, string Extension)? TryDetectOle2ByExtension(string filename)
    {
        var ext = Path.GetExtension(filename)?.ToLowerInvariant();

        return ext switch
        {
            ".doc" => ("application/msword", "doc"),
            ".xls" => ("application/vnd.ms-excel", "xls"),
            ".ppt" => ("application/vnd.ms-powerpoint", "ppt"),
            ".msg" => ("application/vnd.ms-outlook", "msg"),
            _ => null
        };
    }

    private static (string MimeType, string Extension)? TryDetectRiff(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
            return null;

        // Check for RIFF header
        if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F')
            return null;

        // Get the format identifier at offset 8
        var format = Encoding.ASCII.GetString(data.Slice(8, 4));

        return format switch
        {
            "WEBP" => ("image/webp", "webp"),
            "WAVE" => ("audio/wav", "wav"),
            "AVI " => ("video/x-msvideo", "avi"),
            _ => null
        };
    }

    private static bool IsLikelyText(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return false;

        int printableCount = 0;
        foreach (var b in data)
        {
            // Printable ASCII: 0x20-0x7E, plus common whitespace
            if ((b >= 0x20 && b <= 0x7E) || b == 0x09 || b == 0x0A || b == 0x0D)
            {
                printableCount++;
            }
        }

        return (double)printableCount / data.Length >= TextHeuristicThreshold;
    }

    private static MimeDetectionResult CreateResult(
        string detectedContentType,
        string? detectedExtension,
        string detectionMethod,
        string? claimedContentType)
    {
        var isMismatch = !string.IsNullOrEmpty(claimedContentType) &&
                         !claimedContentType.Equals(detectedContentType, StringComparison.OrdinalIgnoreCase);

        var isDangerousMismatch = isMismatch &&
                                  MimeTypes.DangerousTypes.Contains(detectedContentType);

        return new MimeDetectionResult(
            detectedContentType,
            detectedExtension,
            detectionMethod,
            isMismatch,
            isDangerousMismatch
        );
    }
}
