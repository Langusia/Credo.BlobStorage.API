using Credo.BlobStorage.Core.Validation;
using FluentAssertions;
using Xunit;

namespace Credo.BlobStorage.Tests.Validation;

public class FilenameValidatorTests
{
    [Theory]
    [InlineData("report.pdf")]
    [InlineData("my-file.txt")]
    [InlineData("folder/file.doc")]
    [InlineData("a/b/c/d/file.xlsx")]
    [InlineData("file_with_underscore.json")]
    [InlineData("simple")]
    [InlineData("123.456")]
    [InlineData("CamelCase.PDF")]
    public void Validate_ValidFilenames_ReturnsSuccess(string filename)
    {
        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_NullOrEmpty_ReturnsFail(string? filename)
    {
        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("required");
    }

    [Fact]
    public void Validate_TooLong_ReturnsFail()
    {
        var filename = new string('a', 1025);

        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("1024");
    }

    [Theory]
    [InlineData("file\tname.txt")]
    [InlineData("file\nname.txt")]
    [InlineData("file\rname.txt")]
    [InlineData("file\x00name.txt")]
    public void Validate_ControlCharacters_ReturnsFail(string filename)
    {
        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("file\\name.txt")]
    [InlineData("folder\\file.doc")]
    public void Validate_Backslash_ReturnsFail(string filename)
    {
        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("backslash");
    }

    [Theory]
    [InlineData("file name.txt")]
    [InlineData("file@name.txt")]
    [InlineData("file#name.txt")]
    [InlineData("file$name.txt")]
    public void Validate_DisallowedCharacters_ReturnsFail(string filename)
    {
        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("/file.txt")]
    [InlineData("/folder/file.txt")]
    public void Validate_LeadingSlash_ReturnsFail(string filename)
    {
        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("start or end");
    }

    [Theory]
    [InlineData("file.txt/")]
    [InlineData("folder/file.txt/")]
    public void Validate_TrailingSlash_ReturnsFail(string filename)
    {
        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("start or end");
    }

    [Theory]
    [InlineData("folder//file.txt")]
    [InlineData("a//b//c.txt")]
    public void Validate_ConsecutiveSlashes_ReturnsFail(string filename)
    {
        var result = FilenameValidator.Validate(filename);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("consecutive");
    }

    [Theory]
    [InlineData("my%20file.txt", "my file.txt")]
    [InlineData("file%2Fname.txt", "file/name.txt")]
    [InlineData("report.pdf", "report.pdf")]
    public void Normalize_UrlEncodedFilename_DecodesCorrectly(string input, string expected)
    {
        var result = FilenameValidator.Normalize(input);

        result.Should().Be(expected);
    }
}
