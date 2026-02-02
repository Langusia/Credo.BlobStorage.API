using Credo.BlobStorage.Core.Checksums;
using FluentAssertions;
using Xunit;

namespace Credo.BlobStorage.Tests.Services;

public class ChecksumCalculatorTests
{
    private readonly Sha256ChecksumCalculator _calculator = new();

    [Fact]
    public void Compute_ByteArray_ReturnsCorrectHash()
    {
        var data = "Hello, World!"u8.ToArray();

        var hash = _calculator.Compute(data);

        // Known SHA-256 hash for "Hello, World!"
        var expected = "DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F";
        Convert.ToHexString(hash).Should().Be(expected);
    }

    [Fact]
    public void Compute_ReadOnlySpan_ReturnsCorrectHash()
    {
        var data = "Hello, World!"u8.ToArray();

        var hash = _calculator.Compute(data.AsSpan());

        var expected = "DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F";
        Convert.ToHexString(hash).Should().Be(expected);
    }

    [Fact]
    public async Task ComputeAsync_Stream_ReturnsCorrectHash()
    {
        var data = "Hello, World!"u8.ToArray();
        using var stream = new MemoryStream(data);

        var hash = await _calculator.ComputeAsync(stream);

        var expected = "DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F";
        Convert.ToHexString(hash).Should().Be(expected);
    }

    [Fact]
    public void Compute_EmptyData_ReturnsEmptyHash()
    {
        var data = Array.Empty<byte>();

        var hash = _calculator.Compute(data);

        // Known SHA-256 hash for empty input
        var expected = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
        Convert.ToHexString(hash).Should().Be(expected);
    }

    [Fact]
    public async Task ComputeAsync_EmptyStream_ReturnsEmptyHash()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());

        var hash = await _calculator.ComputeAsync(stream);

        var expected = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
        Convert.ToHexString(hash).Should().Be(expected);
    }

    [Fact]
    public async Task ComputeAsync_LargeData_ProcessesIncrementally()
    {
        // Generate 1MB of data
        var data = new byte[1024 * 1024];
        new Random(42).NextBytes(data);
        using var stream = new MemoryStream(data);

        var hash = await _calculator.ComputeAsync(stream);

        // Verify by computing with synchronous method
        var expectedHash = _calculator.Compute(data);
        hash.Should().BeEquivalentTo(expectedHash);
    }

    [Fact]
    public async Task ComputeAsync_CancellationRequested_ThrowsException()
    {
        var data = new byte[1024 * 1024];
        using var stream = new MemoryStream(data);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _calculator.ComputeAsync(stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Compute_SameInput_ReturnsSameHash()
    {
        var data = "Test data for hashing"u8.ToArray();

        var hash1 = _calculator.Compute(data);
        var hash2 = _calculator.Compute(data);

        hash1.Should().BeEquivalentTo(hash2);
    }

    [Fact]
    public void Compute_DifferentInput_ReturnsDifferentHash()
    {
        var data1 = "Data 1"u8.ToArray();
        var data2 = "Data 2"u8.ToArray();

        var hash1 = _calculator.Compute(data1);
        var hash2 = _calculator.Compute(data2);

        hash1.Should().NotBeEquivalentTo(hash2);
    }
}
