using Microsoft.EntityFrameworkCore;

namespace Repository.Helpers;

public static class DbUpdateExceptionX
{
    public static bool IsCustomerIdSlugParentViolation(this DbUpdateException exception)
    {
        return exception.InnerException != null &&
               exception.InnerException.Message.Contains(
                   "duplicate key value violates unique constraint \"ix_hierarchy_customer_id_slug_parent\"");
    }
}