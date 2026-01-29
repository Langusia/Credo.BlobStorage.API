using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Tests.Services;

public class PathBuilderTests
{
    private readonly PathBuilder _pathBuilder;

    public PathBuilderTests()
    {
        var options = Options.Create(new StorageOptions
        {
            RootPath = "/mnt/storage"
        });
        _pathBuilder = new PathBuilder(options);
    }

    [Fact]
    public void GenerateDocId_WithYear_UsesProvidedYear()
    {
        var docId = _pathBuilder.GenerateDocId(2024);

        docId.Should().StartWith("2024-");
        docId.Should().HaveLength(41); // 4 (year) + 1 (-) + 36 (guid)
    }

    [Fact]
    public void GenerateDocId_WithoutYear_UsesCurrentYear()
    {
        var docId = _pathBuilder.GenerateDocId();

        docId.Should().StartWith($"{DateTime.UtcNow.Year}-");
    }

    [Fact]
    public void GenerateDocId_GeneratesUniqueIds()
    {
        var ids = Enumerable.Range(0, 100)
            .Select(_ => _pathBuilder.GenerateDocId())
            .ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ExtractYear_ValidDocId_ReturnsYear()
    {
        var docId = "2024-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e";

        var year = _pathBuilder.ExtractYear(docId);

        year.Should().Be(2024);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExtractYear_NullOrEmpty_ThrowsException(string? docId)
    {
        var act = () => _pathBuilder.ExtractYear(docId!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExtractYear_InvalidFormat_ThrowsException()
    {
        var act = () => _pathBuilder.ExtractYear("invalid-format");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExtractGuid_ValidDocId_ReturnsGuid()
    {
        var docId = "2024-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e";

        var guid = _pathBuilder.ExtractGuid(docId);

        guid.Should().Be("3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e");
    }

    [Fact]
    public void BuildDirectoryPath_ValidDocId_ReturnsCorrectPath()
    {
        var docId = "2024-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e";

        var path = _pathBuilder.BuildDirectoryPath(docId);

        // First 2 chars of guid (without hyphens) = "3f", next 2 = "0d"
        path.Should().Be("/mnt/storage/2024/3f/0d/2024-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e");
    }

    [Fact]
    public void BuildBlobPath_ValidDocId_ReturnsCorrectPath()
    {
        var docId = "2024-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e";

        var path = _pathBuilder.BuildBlobPath(docId, "pdf");

        path.Should().Be("/mnt/storage/2024/3f/0d/2024-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e/blob.pdf");
    }

    [Fact]
    public void BuildBlobPath_ExtensionWithDot_TrimsLeadingDot()
    {
        var docId = "2024-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e";

        var path = _pathBuilder.BuildBlobPath(docId, ".pdf");

        path.Should().EndWith("blob.pdf");
    }

    [Fact]
    public void BuildTempPath_ValidDocId_ReturnsCorrectPath()
    {
        var docId = "2024-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e";

        var path = _pathBuilder.BuildTempPath(docId);

        path.Should().EndWith("blob.tmp");
        path.Should().Contain(docId);
    }
}
