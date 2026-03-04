using System.Text;
using System.Text.RegularExpressions;

namespace Credo.BlobStorage.Core.Validation;

/// <summary>
/// S3-style object key (filename) validation.
/// </summary>
public static class FilenameValidator
{
    private const int MaxLengthBytes = 1024;

    private static readonly Regex ControlCharsRegex = new(
        @"[\x00-\x1F\x7F]",
        RegexOptions.Compiled);

    private static readonly Regex EncodedControlCharsRegex = new(
        @"%(?:0[0-9A-Fa-f]|1[0-9A-Fa-f]|7[Ff])",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates a filename according to S3 object key naming rules.
    /// </summary>
    /// <param name="filename">The filename to validate.</param>
    /// <returns>Validation result with success status and error message if invalid.</returns>
    public static ValidationResult Validate(string? filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return ValidationResult.Fail("Filename is required.");
        }

        // Check UTF-8 byte length
        var byteLength = Encoding.UTF8.GetByteCount(filename);
        if (byteLength > MaxLengthBytes)
        {
            return ValidationResult.Fail($"Filename must not exceed {MaxLengthBytes} bytes when UTF-8 encoded.");
        }

        // Check for control characters (0x00-0x1F and 0x7F)
        if (ControlCharsRegex.IsMatch(filename))
        {
            return ValidationResult.Fail("Filename must not contain control characters.");
        }

        // Check for backslash
        if (filename.Contains('\\'))
        {
            return ValidationResult.Fail("Filename must not contain backslash (\\). Use forward slash (/) instead.");
        }

        // Check for null bytes
        if (filename.Contains('\0'))
        {
            return ValidationResult.Fail("Filename must not contain null characters.");
        }

        // Check for leading or trailing slashes
        if (filename.StartsWith('/') || filename.EndsWith('/'))
        {
            return ValidationResult.Fail("Filename must not start or end with a forward slash.");
        }

        // Check for consecutive slashes
        if (filename.Contains("//"))
        {
            return ValidationResult.Fail("Filename must not contain consecutive forward slashes.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Sanitizes a filename by removing or replacing invalid characters.
    /// Control characters (and their percent-encoded forms like %00) are replaced with underscores,
    /// backslashes with forward slashes, leading/trailing slashes are trimmed,
    /// and consecutive slashes are collapsed.
    /// </summary>
    /// <param name="filename">The filename to sanitize.</param>
    /// <returns>The sanitized filename, or null if the input is null or empty.</returns>
    public static string? Sanitize(string? filename)
    {
        if (string.IsNullOrEmpty(filename))
            return filename;

        // Replace percent-encoded control chars (%00-%1F, %7F) with underscore
        var sanitized = EncodedControlCharsRegex.Replace(filename, "_");

        // Replace literal control chars with underscore
        sanitized = ControlCharsRegex.Replace(sanitized, "_");
        sanitized = sanitized.Replace('\\', '/');

        // Remove leading/trailing slashes
        sanitized = sanitized.Trim('/');

        // Collapse consecutive slashes
        while (sanitized.Contains("//"))
            sanitized = sanitized.Replace("//", "/");

        return sanitized;
    }

    /// <summary>
    /// Normalizes a filename by URL-decoding it.
    /// </summary>
    /// <param name="filename">The potentially URL-encoded filename.</param>
    /// <returns>The decoded filename.</returns>
    public static string Normalize(string filename)
    {
        return Uri.UnescapeDataString(filename);
    }
}
