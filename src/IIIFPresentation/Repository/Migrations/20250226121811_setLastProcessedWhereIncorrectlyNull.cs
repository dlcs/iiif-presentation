using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class setLastProcessedWhereIncorrectlyNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE manifests
SET last_processed = now()
WHERE id NOT IN
      (SELECT DISTINCT manifest_id
       FROM batches
       WHERE finished IS NULL)
AND last_processed IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
