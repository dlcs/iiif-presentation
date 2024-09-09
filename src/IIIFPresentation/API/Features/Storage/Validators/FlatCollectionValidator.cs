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
        
        RuleSet("update", () =>
        {
            RuleFor(f => f.Created).Empty().WithMessage("'Created' cannot be set");
            RuleFor(f => f.Modified).Empty().WithMessage("'Modified' cannot be set");
            RuleFor(f => f.Id).Empty().WithMessage("'Id' cannot be set");
            RuleFor(f => f.Parent).NotEmpty().WithMessage("Updating a collection requires a parent to be set");
            RuleFor(f => f.Context).Empty().WithMessage("'Context' cannot be set");
            RuleFor(f => f.Items).Empty().WithMessage("'Items' cannot be set");
            RuleFor(f => f.Tags).Empty().WithMessage("'Tags' cannot be set");
            RuleFor(f => f.Thumbnail).Empty().WithMessage("'Thumbnail' cannot be set");
            RuleFor(f => f.Type).Empty().WithMessage("'Type' cannot be set");
            RuleFor(f => f.View).Empty().WithMessage("'View' cannot be set");
            RuleFor(f => f.ItemsOrder).Empty().WithMessage("'Items order' cannot be set");
            RuleFor(f => f.CreatedBy).Empty().WithMessage("'Created  by' cannot be set");
            RuleFor(f => f.ModifiedBy).Empty().WithMessage("'Modified by' cannot be set");
            RuleFor(f => f.PartOf).Empty().WithMessage("'Part of' cannot be set");
            RuleFor(f => f.SeeAlso).Empty().WithMessage("'See also' by cannot be set");
            RuleFor(f => f.TotalItems).Empty().WithMessage("'Total items' by cannot be set");

            RuleFor(f => f.Behavior).Must(f => f.Contains("storage-collection"))
                .WithMessage("'Behavior' must contain 'storage-collection' when updating");
        });
    }
}