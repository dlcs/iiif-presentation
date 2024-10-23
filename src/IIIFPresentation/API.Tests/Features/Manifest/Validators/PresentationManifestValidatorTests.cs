using API.Features.Manifest.Validators;
using FluentValidation.TestHelper;
using Models.API.Manifest;

namespace API.Tests.Features.Manifest.Validators;

public class PresentationManifestValidatorTests
{
    private readonly PresentationManifestValidator sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Slug_Required(string? parent)
    {
        var manifest = new PresentationManifest { Slug = parent };
        
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
}