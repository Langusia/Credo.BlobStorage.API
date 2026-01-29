namespace Credo.BlobStorage.Core.Checksums;

/// <summary>
/// Interface for computing file checksums.
/// </summary>
public interface IChecksumCalculator
{
    /// <summary>
    /// Computes SHA-256 hash incrementally from stream.
    /// Does NOT buffer entire file.
    /// </summary>
    Task<byte[]> ComputeAsync(Stream stream, CancellationToken ct = default);

    /// <summary>
    /// Computes SHA-256 hash from a byte array.
    /// </summary>
    byte[] Compute(byte[] data);

    /// <summary>
    /// Computes SHA-256 hash from a ReadOnlySpan.
    /// </summary>
    byte[] Compute(ReadOnlySpan<byte> data);
}
