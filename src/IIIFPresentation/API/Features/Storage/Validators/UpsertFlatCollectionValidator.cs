using API.Features.Storage.Helpers;
using FluentValidation;
using Models.API.Collection.Upsert;

namespace API.Features.Storage.Validators;

public class UpsertFlatCollectionValidator : AbstractValidator<UpsertFlatCollection>
{
    public UpsertFlatCollectionValidator()
    {
        RuleFor(f => f.Parent).NotEmpty().WithMessage("Requires a 'parent' to be set");
        RuleFor(f => f.Slug).NotEmpty().WithMessage("Requires a 'slug' to be set");
        
        RuleFor(f => f.Behavior).Must(f => f.IsStorageCollection())
            .WithMessage("'Behavior' must contain 'storage-collection' when updating");
    }
}