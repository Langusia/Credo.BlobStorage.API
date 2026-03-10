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
    [InlineData("მარიამი.jpg")]           // Georgian characters
    [InlineData("Фото.jpg")]              // Cyrillic characters
    [InlineData("file name.jpg")]         // Spaces
    [InlineData("file(1).jpg")]           // Parentheses
    [InlineData("report (final).pdf")]    // Spaces and parentheses
    [InlineData("café.txt")]              // Accented characters
    [InlineData("文档.pdf")]               // Chinese characters
    [InlineData("file@name.txt")]         // @ symbol
    [InlineData("file#name.txt")]         // # symbol
    [InlineData("file$name.txt")]         // $ symbol
    [InlineData("data;export.csv")]       // Semicolon
    [InlineData("file^v2.txt")]           // Caret
    [InlineData("file,backup.txt")]       // Comma
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

    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("folder/file.doc", "folder/file.doc")]
    public void Sanitize_ValidFilename_ReturnsUnchanged(string input, string expected)
    {
        var result = FilenameValidator.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("file\tname.txt", "file_name.txt")]
    [InlineData("file\nname.txt", "file_name.txt")]
    [InlineData("file\rname.txt", "file_name.txt")]
    [InlineData("file\x00name.txt", "file_name.txt")]
    [InlineData("file\x1Fname.txt", "file_name.txt")]
    public void Sanitize_ControlCharacters_ReplacedWithUnderscore(string input, string expected)
    {
        var result = FilenameValidator.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("50%0001.jpg", "50_1.jpg")]
    [InlineData("50%0002.jpg", "50_2.jpg")]
    [InlineData("file%1Fname.txt", "file_name.txt")]
    [InlineData("file%7fname.txt", "file_name.txt")]
    public void Sanitize_EncodedControlCharacters_ReplacedWithUnderscore(string input, string expected)
    {
        var result = FilenameValidator.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("folder\\file.doc", "folder/file.doc")]
    [InlineData("a\\b\\c.txt", "a/b/c.txt")]
    public void Sanitize_Backslashes_ReplacedWithForwardSlash(string input, string expected)
    {
        var result = FilenameValidator.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/file.txt", "file.txt")]
    [InlineData("file.txt/", "file.txt")]
    [InlineData("/folder/file.txt/", "folder/file.txt")]
    public void Sanitize_LeadingTrailingSlashes_Trimmed(string input, string expected)
    {
        var result = FilenameValidator.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("folder//file.txt", "folder/file.txt")]
    [InlineData("a///b//c.txt", "a/b/c.txt")]
    public void Sanitize_ConsecutiveSlashes_Collapsed(string input, string expected)
    {
        var result = FilenameValidator.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_NullOrEmpty_ReturnsAsIs(string? input)
    {
        var result = FilenameValidator.Sanitize(input);

        result.Should().Be(input);
    }

    [Fact]
    public void Sanitize_ResultPassesValidation()
    {
        var dirty = "/folder\\sub\tfolder//file\nname.txt/";

        var sanitized = FilenameValidator.Sanitize(dirty);

        sanitized.Should().NotBeNull();
        FilenameValidator.Validate(sanitized!).IsValid.Should().BeTrue();
    }
}
