using Core.Infrastructure;
using FluentValidation;
using Models.API.Collection;

namespace API.Features.Storage.Validators;

public class RootCollectionValidator : AbstractValidator<PresentationCollection>
{
    public RootCollectionValidator()
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
    }
}
