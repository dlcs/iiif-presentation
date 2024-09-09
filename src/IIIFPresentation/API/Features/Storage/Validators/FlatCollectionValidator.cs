using FluentValidation;
using Models.API.Collection;

namespace API.Features.Storage.Validators;

public class FlatCollectionValidator : AbstractValidator<FlatCollection>
{
    public FlatCollectionValidator()
    {
        RuleSet("create", () =>
        {
            RuleFor(f => f.Created).Empty().WithMessage("'Created' cannot be set");
            RuleFor(f => f.Modified).Empty().WithMessage("'Modified' cannot be set");
            RuleFor(f => f.Id).Empty().WithMessage("'Id' cannot be set");
            RuleFor(f => f.Parent).NotEmpty().WithMessage("Creating a new collection requires a parent");
        });
    }
}