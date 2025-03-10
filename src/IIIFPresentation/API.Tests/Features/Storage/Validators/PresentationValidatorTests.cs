using API.Features.Storage.Validators;
using API.Infrastructure.Validation;
using FluentValidation.TestHelper;
using Models.API.Manifest;

namespace API.Tests.Features.Storage.Validators;

public class PresentationValidatorTests
{
    private readonly PresentationValidator sut = new();

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
