using FluentValidation;
using Models.API.Collection.Update;

namespace API.Features.Storage.Validators;

public class UpdateFlatCollectionValidator : AbstractValidator<UpdateFlatCollection>
{
    public UpdateFlatCollectionValidator()
    {
        RuleSet("update", () =>
        {
            RuleFor(f => f.Parent).NotEmpty().WithMessage("Updating a collection requires a parent to be set");

            RuleFor(f => f.Behavior).Must(f => f.Contains("storage-collection"))
                .WithMessage("'Behavior' must contain 'storage-collection' when updating");
        });
    }
}