using FluentValidation;
using Models.API.Collection;

namespace API.Features.Storage.Validators;

public class PresentationCollectionValidator : AbstractValidator<PresentationCollection>
{
    public PresentationCollectionValidator()
    {
        RuleFor(c => c).SetValidator(new PresentationValidator());
    }
}
