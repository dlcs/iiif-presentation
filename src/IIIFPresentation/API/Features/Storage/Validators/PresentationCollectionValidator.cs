using API.Infrastructure.Validation;
using Core.Infrastructure;
using FluentValidation;
using Models;
using Models.API.Collection;

namespace API.Features.Storage.Validators;

public class PresentationCollectionValidator : AbstractValidator<PresentationCollection>
{
    public const string StandardRuleSet = "standard";
    public const string RootRuleSet = "root";
    public PresentationCollectionValidator()
    {
        RuleSet(StandardRuleSet, () =>
        {
            RuleFor(pc => pc.Parent)
                .NotEmpty()
                .WithMessage("Requires a 'parent' to be set");
            
            RuleFor(pc => pc.Slug)
                .NotEmpty()
                .WithMessage("Requires a 'slug' to be set")
                .Must(slug => !SpecConstants.ProhibitedSlugs.Contains(slug!))
                .WithMessage("'slug' cannot be one of prohibited terms: '{PropertyValue}'");
        });
     
        RuleSet(RootRuleSet, () =>
        {
            RuleFor(pc => pc.Parent)
                .Empty()
                .WithMessage("Cannot set 'parent' for root collection");

            RuleFor(pc => pc.Slug)
                .Empty()
                .WithMessage("Cannot set 'slug' for root collection");
            
            RuleFor(pc => pc.Behavior)
                .Must(behaviors => behaviors?.Contains(Behavior.IsStorageCollection) ?? false)
                .WithMessage($"Root must have '{Behavior.IsStorageCollection}' behavior");
        });
    }
}
