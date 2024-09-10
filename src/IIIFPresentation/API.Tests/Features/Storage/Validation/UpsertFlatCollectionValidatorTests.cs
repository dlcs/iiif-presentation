using API.Features.Storage.Validators;
using FluentValidation.TestHelper;
using Models.API.Collection.Upsert;

namespace API.Tests.Features.Storage.Validation;

public class UpsertFlatCollectionValidatorTests
{
    private readonly UpsertFlatCollectionValidator sut = new();

    [Fact]
    public void NewUpsertFlatCollectionValidator_MustHave_StorageCollection()
    {
        var upsertFlatCollection = new UpsertFlatCollection()
        {
            Parent = "parent",
            Slug = "slug"
        };
        
        var result = sut.TestValidate(upsertFlatCollection);
        result.ShouldHaveValidationErrorFor(f => f.Behavior);
    }
}