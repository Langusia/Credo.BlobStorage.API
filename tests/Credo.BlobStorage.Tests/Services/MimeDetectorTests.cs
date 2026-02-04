using Credo.BlobStorage.Core.Mime;
using FluentAssertions;
using Xunit;

namespace Credo.BlobStorage.Tests.Services;

public class MimeDetectorTests
{
    private readonly MimeDetector _detector = new();

    [Fact]
    public void Detect_PngMagicBytes_ReturnsPng()
    {
        // PNG magic bytes: 89 50 4E 47
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var result = _detector.Detect(header);

        result.DetectedContentType.Should().Be("image/png");
        result.DetectedExtension.Should().Be("png");
        result.DetectionMethod.Should().Be("magic");
    }

    [Fact]
    public void Detect_JpegMagicBytes_ReturnsJpeg()
    {
        // JPEG magic bytes: FF D8 FF
        var header = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

        var result = _detector.Detect(header);

        result.DetectedContentType.Should().Be("image/jpeg");
        result.DetectedExtension.Should().Be("jpg");
        result.DetectionMethod.Should().Be("magic");
    }

    [Fact]
    public void Detect_PdfMagicBytes_ReturnsPdf()
    {
        // PDF magic bytes: 25 50 44 46 (%PDF)
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

        var result = _detector.Detect(header);

        result.DetectedContentType.Should().Be("application/pdf");
        result.DetectedExtension.Should().Be("pdf");
        result.DetectionMethod.Should().Be("magic");
    }

    [Fact]
    public void Detect_ZipMagicBytes_ReturnsZip()
    {
        // ZIP magic bytes: 50 4B 03 04
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00 };

        var result = _detector.Detect(header);

        result.DetectedContentType.Should().Be("application/zip");
        result.DetectedExtension.Should().Be("zip");
        result.DetectionMethod.Should().Be("magic");
    }

    [Fact]
    public void Detect_UnknownMagicBytes_WithExtension_UsesExtension()
    {
        var header = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        var result = _detector.Detect(header, "document.txt");

        result.DetectedContentType.Should().Be("text/plain");
        result.DetectedExtension.Should().Be("txt");
        result.DetectionMethod.Should().Be("extension");
    }

    [Fact]
    public void Detect_UnknownMagicBytes_WithClaimedType_UsesClaimedType()
    {
        var header = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        var result = _detector.Detect(header, null, "application/json");

        result.DetectedContentType.Should().Be("application/json");
        result.DetectedExtension.Should().Be("json");
        result.DetectionMethod.Should().Be("header");
    }

    [Fact]
    public void Detect_TextContent_DetectedAsText()
    {
        var header = "Hello, this is a plain text file content."u8.ToArray();

        var result = _detector.Detect(header);

        result.DetectedContentType.Should().Be("text/plain");
        result.DetectedExtension.Should().Be("txt");
        result.DetectionMethod.Should().Be("heuristic");
    }

    [Fact]
    public void Detect_UnknownBinaryContent_ReturnsFallback()
    {
        // Random binary that doesn't match any signatures and isn't text
        var header = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };

        var result = _detector.Detect(header);

        result.DetectedContentType.Should().Be("application/octet-stream");
        result.DetectedExtension.Should().Be("bin");
        result.DetectionMethod.Should().Be("fallback");
    }

    [Fact]
    public void Detect_ClaimedMismatch_ReturnsIsMismatch()
    {
        // PDF magic bytes
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

        var result = _detector.Detect(header, "document.pdf", "image/jpeg");

        result.DetectedContentType.Should().Be("application/pdf");
        result.IsMismatch.Should().BeTrue();
        result.IsDangerousMismatch.Should().BeFalse();
    }

    [Fact]
    public void Detect_DangerousMismatch_ReturnsDangerousFlag()
    {
        // EXE magic bytes: MZ
        var header = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };

        var result = _detector.Detect(header, "document.pdf", "application/pdf");

        result.DetectedContentType.Should().Be("application/x-msdownload");
        result.IsMismatch.Should().BeTrue();
        result.IsDangerousMismatch.Should().BeTrue();
    }

    [Fact]
    public void Detect_NoMismatch_WhenClaimedMatchesDetected()
    {
        // PNG magic bytes
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var result = _detector.Detect(header, "image.png", "image/png");

        result.DetectedContentType.Should().Be("image/png");
        result.IsMismatch.Should().BeFalse();
        result.IsDangerousMismatch.Should().BeFalse();
    }

    [Fact]
    public void Detect_EmptyContent_ReturnsFallback()
    {
        var result = _detector.Detect(ReadOnlySpan<byte>.Empty);

        result.DetectedContentType.Should().Be("application/octet-stream");
        result.DetectionMethod.Should().Be("fallback");
    }

    [Fact]
    public void Detect_GifMagicBytes_ReturnsGif()
    {
        // GIF magic bytes: 47 49 46 38 (GIF8)
        var header = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00 };

        var result = _detector.Detect(header);

        result.DetectedContentType.Should().Be("image/gif");
        result.DetectedExtension.Should().Be("gif");
    }
}
