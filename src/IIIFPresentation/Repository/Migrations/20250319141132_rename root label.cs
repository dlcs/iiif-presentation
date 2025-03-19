using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class renamerootlabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update any 'root' collections that have the default (repository root) label
            migrationBuilder.Sql(@"
UPDATE collections
SET label = '{""en"": [""IIIF Home""]}'
WHERE id = 'root'
  AND label = '{""en"": [""(repository root)""]}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
