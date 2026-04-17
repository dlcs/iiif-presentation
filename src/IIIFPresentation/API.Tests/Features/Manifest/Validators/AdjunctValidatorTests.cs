using API.Features.Manifest.Validators;
using FluentValidation.TestHelper;
using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Tests.Features.Manifest.Validators;

public class AdjunctValidatorTests
{
    private readonly AdjunctValidator sut = new();

    [Fact]
    public void Id_Required_ErrorWhenMissing()
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.ExternalId] = "https://example.com"
        };

        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(a => a[AssetProperties.Id])
            .WithErrorMessage("Adjunct 'id' must not be empty");
    }

    [Fact]
    public void Id_Required_ErrorWhenEmptyString()
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.Id] = string.Empty,
            [AdjunctProperties.ExternalId] = "https://example.com"
        };

        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(a => a[AssetProperties.Id])
            .WithErrorMessage("Adjunct 'id' must not be empty");
    }

    [Fact]
    public void Id_Required_NoErrorWhenValidString()
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.Id] = "my-adjunct",
            [AdjunctProperties.ExternalId] = "https://example.com"
        };

        var result = sut.TestValidate(adjunct);
        result.ShouldNotHaveValidationErrorFor(a => a[AssetProperties.Id]);
    }

    [Theory]
    [InlineData("foo/bar")]
    [InlineData("foo=bar")]
    [InlineData("foo,bar")]
    public void Id_ProhibitedCharacters_ErrorWhenPresent(string id)
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.Id] = id,
            [AdjunctProperties.ExternalId] = "https://example.com"
        };

        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(a => a[AssetProperties.Id])
            .WithErrorMessage("Adjunct 'id' contains a prohibited character. Cannot contain any of: '/', '=', ','");
    }

    [Fact]
    public void Id_ProhibitedCharacters_NoErrorWhenIdIsValid()
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.Id] = "my-adjunct",
            [AdjunctProperties.ExternalId] = "https://example.com"
        };

        var result = sut.TestValidate(adjunct);
        result.ShouldNotHaveValidationErrorFor(a => a[AssetProperties.Id]);
    }

    [Fact]
    public void Id_ProhibitedCharacters_RuleSkippedWhenIdMissing()
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.ExternalId] = "https://example.com"
        };

        var result = sut.TestValidate(adjunct);
        result.Errors.Should().NotContain(e =>
            e.ErrorMessage ==
            "Adjunct 'id' contains a prohibited character. Cannot contain any of: '/', '=', ','");
    }

    [Fact]
    public void ExternalIdOrOrigin_Required_ErrorWhenNeitherPresent()
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.Id] = "my-adjunct"
        };

        var result = sut.TestValidate(adjunct);
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Adjunct must have either 'externalId' or 'origin' set");
    }

    [Fact]
    public void ExternalIdOrOrigin_NoErrorWhenExternalIdPresent()
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.Id] = "my-adjunct",
            [AdjunctProperties.ExternalId] = "https://example.com"
        };

        var result = sut.TestValidate(adjunct);
        result.Errors.Should().NotContain(e =>
            e.ErrorMessage == "Adjunct must have either 'externalId' or 'origin' set");
    }

    [Fact]
    public void ExternalIdOrOrigin_NoErrorWhenOriginPresent()
    {
        var adjunct = new JObject
        {
            [AdjunctProperties.Id] = "my-adjunct",
            [AdjunctProperties.Origin] = "https://example.com/origin"
        };

        var result = sut.TestValidate(adjunct);
        result.Errors.Should().NotContain(e =>
            e.ErrorMessage == "Adjunct must have either 'externalId' or 'origin' set");
    }
}
