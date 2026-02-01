using System.Net;
using System.Text.RegularExpressions;

namespace Credo.BlobStorage.Core.Validation;

/// <summary>
/// S3-style bucket name validation.
/// </summary>
public static class BucketNameValidator
{
    private const int MinLength = 3;
    private const int MaxLength = 63;

    private static readonly Regex AllowedCharsRegex = new(
        "^[a-z0-9.-]+$",
        RegexOptions.Compiled);

    private static readonly Regex Ipv4LikeRegex = new(
        @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates a bucket name according to S3 naming rules.
    /// </summary>
    /// <param name="name">The bucket name to validate.</param>
    /// <returns>Validation result with success status and error message if invalid.</returns>
    public static ValidationResult Validate(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return ValidationResult.Fail("Bucket name is required.");
        }

        if (name.Length < MinLength)
        {
            return ValidationResult.Fail($"Bucket name must be at least {MinLength} characters.");
        }

        if (name.Length > MaxLength)
        {
            return ValidationResult.Fail($"Bucket name must not exceed {MaxLength} characters.");
        }

        // Must contain only lowercase letters, digits, dots, and hyphens
        if (!AllowedCharsRegex.IsMatch(name))
        {
            return ValidationResult.Fail("Bucket name must contain only lowercase letters, digits, dots (.), and hyphens (-).");
        }

        // Must start with a letter or digit
        if (!char.IsLetterOrDigit(name[0]) || char.IsUpper(name[0]))
        {
            return ValidationResult.Fail("Bucket name must start with a lowercase letter or digit.");
        }

        // Must end with a letter or digit
        if (!char.IsLetterOrDigit(name[^1]) || char.IsUpper(name[^1]))
        {
            return ValidationResult.Fail("Bucket name must end with a lowercase letter or digit.");
        }

        // No consecutive dots
        if (name.Contains(".."))
        {
            return ValidationResult.Fail("Bucket name must not contain consecutive dots (..).");
        }

        // Must not look like an IPv4 address
        if (IPAddress.TryParse(name, out _))
        {
            return ValidationResult.Fail("Bucket name must not be formatted as an IP address.");
        }

        // Additional IPv4-like check (e.g., 192.168.1.1 pattern)
        if (Ipv4LikeRegex.IsMatch(name))
        {
            return ValidationResult.Fail("Bucket name must not be formatted as an IP address.");
        }

        // Must not start with "xn--" (Punycode prefix)
        if (name.StartsWith("xn--", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Fail("Bucket name must not start with 'xn--'.");
        }

        // Must not end with "-s3alias"
        if (name.EndsWith("-s3alias", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Fail("Bucket name must not end with '-s3alias'.");
        }

        // Must not end with "--ol-s3"
        if (name.EndsWith("--ol-s3", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Fail("Bucket name must not end with '--ol-s3'.");
        }

        return ValidationResult.Success();
    }
}
