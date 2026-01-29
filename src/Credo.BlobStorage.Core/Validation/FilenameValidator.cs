using System.Text;
using System.Text.RegularExpressions;

namespace Credo.BlobStorage.Core.Validation;

/// <summary>
/// S3-style object key (filename) validation.
/// </summary>
public static partial class FilenameValidator
{
    private const int MaxLengthBytes = 1024;

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
        if (ControlCharsRegex().IsMatch(filename))
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

        // Check allowed characters: a-z A-Z 0-9 . _ - /
        if (!AllowedCharsRegex().IsMatch(filename))
        {
            return ValidationResult.Fail("Filename must contain only alphanumeric characters, dots (.), underscores (_), hyphens (-), and forward slashes (/).");
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
    /// Normalizes a filename by URL-decoding it.
    /// </summary>
    /// <param name="filename">The potentially URL-encoded filename.</param>
    /// <returns>The decoded filename.</returns>
    public static string Normalize(string filename)
    {
        return Uri.UnescapeDataString(filename);
    }

    [GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial Regex ControlCharsRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9._\-/]+$")]
    private static partial Regex AllowedCharsRegex();
}
