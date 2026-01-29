using Credo.BlobStorage.Core.Validation;
using FluentAssertions;

namespace Credo.BlobStorage.Tests.Validation;

public class BucketNameValidatorTests
{
    [Theory]
    [InlineData("valid-bucket")]
    [InlineData("my.bucket.name")]
    [InlineData("bucket123")]
    [InlineData("123bucket")]
    [InlineData("abc")]
    [InlineData("a-b")]
    [InlineData("a.b")]
    public void Validate_ValidBucketNames_ReturnsSuccess(string name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_NullOrEmpty_ReturnsFail(string? name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("required");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public void Validate_TooShort_ReturnsFail(string name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at least 3");
    }

    [Fact]
    public void Validate_TooLong_ReturnsFail()
    {
        var name = new string('a', 64);

        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("63");
    }

    [Theory]
    [InlineData("Invalid-Bucket")]
    [InlineData("UPPERCASE")]
    [InlineData("MixedCase")]
    public void Validate_UppercaseLetters_ReturnsFail(string name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("lowercase");
    }

    [Theory]
    [InlineData("bucket_name")]
    [InlineData("bucket@name")]
    [InlineData("bucket name")]
    [InlineData("bucket!name")]
    public void Validate_InvalidCharacters_ReturnsFail(string name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("-bucket")]
    [InlineData(".bucket")]
    public void Validate_StartsWithNonAlphanumeric_ReturnsFail(string name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("start with");
    }

    [Theory]
    [InlineData("bucket-")]
    [InlineData("bucket.")]
    public void Validate_EndsWithNonAlphanumeric_ReturnsFail(string name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("end with");
    }

    [Theory]
    [InlineData("bucket..name")]
    [InlineData("my..bucket")]
    public void Validate_ConsecutiveDots_ReturnsFail(string name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("consecutive dots");
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("255.255.255.255")]
    public void Validate_IPv4Address_ReturnsFail(string name)
    {
        var result = BucketNameValidator.Validate(name);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("IP address");
    }

    [Fact]
    public void Validate_StartsWithXnDash_ReturnsFail()
    {
        var result = BucketNameValidator.Validate("xn--bucket");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("xn--");
    }

    [Fact]
    public void Validate_EndsWithS3Alias_ReturnsFail()
    {
        var result = BucketNameValidator.Validate("bucket-s3alias");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("-s3alias");
    }

    [Fact]
    public void Validate_EndsWithOlS3_ReturnsFail()
    {
        var result = BucketNameValidator.Validate("bucket--ol-s3");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("--ol-s3");
    }
}
