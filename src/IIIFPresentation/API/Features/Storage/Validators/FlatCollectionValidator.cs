using FluentValidation;
using Models.API.Collection;

namespace API.Features.Storage.Validators;

public class FlatCollectionValidator : AbstractValidator<FlatCollection>
{
    public FlatCollectionValidator()
    {
        RuleSet("create", () =>
        {
            RuleFor(a => a.Created).Empty().WithMessage("Created cannot be set");
            RuleFor(a => a.Modified).Empty().WithMessage("Modified cannot be set");
            RuleFor(a => a.Id).Empty().WithMessage("Id cannot be set");
            RuleFor(a => a.Parent).NotEmpty().WithMessage("Creating a new collection requires a parent");
        });
    }
}