using System.Security.Cryptography;

namespace Credo.BlobStorage.Core.Checksums;

/// <summary>
/// SHA-256 checksum calculator with incremental stream processing.
/// </summary>
public class Sha256ChecksumCalculator : IChecksumCalculator
{
    private const int DefaultBufferSize = 81920; // 80KB buffer

    /// <inheritdoc />
    public async Task<byte[]> ComputeAsync(Stream stream, CancellationToken ct = default)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[DefaultBufferSize];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            sha256.AppendData(buffer, 0, bytesRead);
        }

        return sha256.GetHashAndReset();
    }

    /// <inheritdoc />
    public byte[] Compute(byte[] data)
    {
        return SHA256.HashData(data);
    }

    /// <inheritdoc />
    public byte[] Compute(ReadOnlySpan<byte> data)
    {
        return SHA256.HashData(data);
    }
}
