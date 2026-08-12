using Microsoft.EntityFrameworkCore;
using Repository.Helpers;

namespace Repository.Tests.Helpers;

public class DbUpdateExceptionXTests
{
    [Fact]
    public void IsCollectionPrimaryKeyViolation_ReturnsTrue_ForCollectionsPkViolation()
    {
        var exception = new DbUpdateException("save failed",
            new Exception("23505: duplicate key value violates unique constraint \"pk_collections\""));

        exception.IsCollectionPrimaryKeyViolation().Should().BeTrue();
    }

    [Fact]
    public void IsCollectionPrimaryKeyViolation_ReturnsFalse_ForManifestsPkViolation()
    {
        var exception = new DbUpdateException("save failed",
            new Exception("23505: duplicate key value violates unique constraint \"pk_manifests\""));

        exception.IsCollectionPrimaryKeyViolation().Should().BeFalse();
    }

    [Fact]
    public void IsCollectionPrimaryKeyViolation_ReturnsFalse_WhenNoInnerException()
    {
        var exception = new DbUpdateException("save failed");

        exception.IsCollectionPrimaryKeyViolation().Should().BeFalse();
    }

    [Fact]
    public void IsManifestPrimaryKeyViolation_ReturnsTrue_ForManifestsPkViolation()
    {
        var exception = new DbUpdateException("save failed",
            new Exception("23505: duplicate key value violates unique constraint \"pk_manifests\""));

        exception.IsManifestPrimaryKeyViolation().Should().BeTrue();
    }

    [Fact]
    public void IsCustomerIdSlugParentViolation_ReturnsTrue_ForHierarchyIndexViolation()
    {
        var exception = new DbUpdateException("save failed",
            new Exception(
                "23505: duplicate key value violates unique constraint \"ix_hierarchy_customer_id_slug_parent\""));

        exception.IsCustomerIdSlugParentViolation().Should().BeTrue();
    }
}
