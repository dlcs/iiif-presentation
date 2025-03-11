using API.Features.Storage.Validators;
using API.Infrastructure.Validation;
using FluentValidation.Internal;
using FluentValidation.TestHelper;
using Models.API.Collection;

namespace API.Tests.Features.Storage.Validators;

public class PresentationCollectionValidatorTests
{
    private readonly PresentationCollectionValidator sut = new();

    private readonly Action<ValidationStrategy<PresentationCollection>> standard = opts =>
        opts.IncludeRuleSets(PresentationCollectionValidator.StandardRuleSet);
    
    private readonly Action<ValidationStrategy<PresentationCollection>> root = opts =>
        opts.IncludeRuleSets(PresentationCollectionValidator.RootRuleSet);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void StandardRuleset_Slug_Required(string? slug)
    {
        var presentationCollection = new PresentationCollection { Slug = slug };

        sut.TestValidate(presentationCollection, opts =>
                opts.IncludeRuleSets(PresentationCollectionValidator.StandardRuleSet))
            .ShouldHaveValidationErrorFor(c => c.Slug)
            .WithErrorMessage("Requires a 'slug' to be set");
    }
    
    public static TheoryData<string> ProhibitedSlugProvider =>
        new(SpecConstants.ProhibitedSlugs);

    [Theory]
    [MemberData(nameof(ProhibitedSlugProvider))]
    public void StandardRuleset_Slug_CannotBeProhibited(string? slug)
    {
        var presentationCollection = new PresentationCollection { Slug = slug };

        sut.TestValidate(presentationCollection, standard)
            .ShouldHaveValidationErrorFor(c => c.Slug)
            .WithErrorMessage($"'slug' cannot be one of prohibited terms: '{slug}'");
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void StandardRuleset_Parent_Required(string? parent)
    {
        var presentationCollection = new PresentationCollection { Parent = parent, Slug = "foo" };

        sut.TestValidate(presentationCollection, standard)
            .ShouldHaveValidationErrorFor(c => c.Parent)
            .WithErrorMessage("Requires a 'parent' to be set");
    }
    
    [Fact]
    public void RootRuleset_Slug_CannotHaveValue()
    {
        var presentationCollection = new PresentationCollection { Slug = "s" };

        sut.TestValidate(presentationCollection, root)
            .ShouldHaveValidationErrorFor(c => c.Slug)
            .WithErrorMessage("Cannot set 'slug' for root collection");
    }
    
    [Fact]
    public void RootRuleset_Parent_CannotHaveValue()
    {
        var presentationCollection = new PresentationCollection { Parent = "p" };

        sut.TestValidate(presentationCollection, root)
            .ShouldHaveValidationErrorFor(c => c.Parent)
            .WithErrorMessage("Cannot set 'parent' for root collection");
    }

    public static TheoryData<List<string>?> InvalidBehaviors =>
        new TheoryData<List<string>>(null, new List<string>(), new List<string> { "anything" });

    [Theory]
    [MemberData(nameof(InvalidBehaviors))]
    public void RootRuleset_MustHaveStorageCollectionBehavior(List<string>? behavior)
    {
        var presentationCollection = new PresentationCollection { Behavior = behavior };

        sut.TestValidate(presentationCollection, root)
            .ShouldHaveValidationErrorFor(c => c.Behavior)
            .WithErrorMessage("Root must have 'storage-collection' behavior");
    }
}
