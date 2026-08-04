using API.Features.Storage.Validators;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Models.API.General;
using Models.API.Manifest;
using Services.Manifests.Settings;

namespace API.Tests.Features.Storage.Validators;

public class PresentationValidatorTests
{
    private readonly PresentationValidator sut = new(Options.Create(new ServicesSettings()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Slug_Required(string? slug)
    {
        var manifest = new PresentationManifest { Slug = slug };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Slug);
    }
    
    public static TheoryData<string> ProhibitedSlugProvider =>
        new(SpecConstants.ProhibitedSlugs);
    
    [Theory]
    [MemberData(nameof(ProhibitedSlugProvider))]
    public void Slug_CannotBeProhibited(string? slug)
    {
        var manifest = new PresentationManifest { Slug = slug };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Slug);
    }
    
    [Theory]
    [InlineData("foo/bar")]
    [InlineData("/foo")]
    [InlineData("foo/")]
    public void Slug_CannotContainForwardSlash(string slug)
    {
        var manifest = new PresentationManifest { Slug = slug };

        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Slug);
    }

    [Theory]
    [InlineData("https://example.com/foo")]
    [InlineData("http://example.org")]
    public void Slug_CannotBeFullyQualifiedUri(string slug)
    {
        var manifest = new PresentationManifest { Slug = slug };

        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Slug);
    }

    [Theory]
    [InlineData("normal-slug")]
    [InlineData("example.com")]
    public void Slug_ValidValues_NoValidationError(string slug)
    {
        var manifest = new PresentationManifest { Slug = slug, PublicId = "https://example.com/1/manifests/foo" };

        var result = sut.TestValidate(manifest);
        result.ShouldNotHaveValidationErrorFor(m => m.Slug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parent_Required(string? parent)
    {
        var manifest = new PresentationManifest { Parent = parent };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Parent);
    }
    
    [Fact]
    public void Parent_NotWellFormedUri()
    {
        var manifest = new PresentationManifest { Parent = "notaUri" };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Parent);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PublicId_Required(string? publicId)
    {
        var manifest = new PresentationManifest { PublicId = publicId };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.PublicId);
    }
}
