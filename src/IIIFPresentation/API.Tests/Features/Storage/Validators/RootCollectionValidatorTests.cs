using API.Features.Storage.Validators;
using FluentValidation.TestHelper;
using Models.API.Collection;

namespace API.Tests.Features.Storage.Validators;

public class RootCollectionValidatorTests
{
    private readonly RootCollectionValidator sut = new();

    [Fact]
    public void RootRuleset_Slug_CannotHaveValue()
    {
        var presentationCollection = new PresentationCollection { Slug = "s" };

        sut.TestValidate(presentationCollection)
            .ShouldHaveValidationErrorFor(c => c.Slug)
            .WithErrorMessage("Cannot set 'slug' for root collection");
    }
    
    [Fact]
    public void RootRuleset_Parent_CannotHaveValue()
    {
        var presentationCollection = new PresentationCollection { Parent = "p" };

        sut.TestValidate(presentationCollection)
            .ShouldHaveValidationErrorFor(c => c.Parent)
            .WithErrorMessage("Cannot set 'parent' for root collection");
    }

    public static TheoryData<List<string>?> InvalidBehaviors =>
        new(null, new List<string>(), new List<string> { "anything" });

    [Theory]
    [MemberData(nameof(InvalidBehaviors))]
    public void RootRuleset_MustHaveStorageCollectionBehavior(List<string>? behavior)
    {
        var presentationCollection = new PresentationCollection { Behavior = behavior };

        sut.TestValidate(presentationCollection)
            .ShouldHaveValidationErrorFor(c => c.Behavior)
            .WithErrorMessage("Root must have 'storage-collection' behavior");
    }
}
