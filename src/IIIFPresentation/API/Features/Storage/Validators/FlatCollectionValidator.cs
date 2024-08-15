using FluentValidation;
using Models.Response;

namespace API.Features.Storage.Validators;

public class FlatCollectionValidator : AbstractValidator<FlatCollection>
{
    public FlatCollectionValidator()
    {
        RuleSet("create", () =>
        {
            RuleFor(a => a.Created).Empty().WithMessage("Created cannot be set");
            RuleFor(a => a.Modified).Empty().WithMessage("Modified cannot be set");
        });
        
        

    }
}