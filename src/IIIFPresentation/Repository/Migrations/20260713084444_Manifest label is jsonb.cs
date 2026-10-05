using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class Manifestlabelisjsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Postgres cannot implicitly cast text -> jsonb, so an explicit USING clause is required
            migrationBuilder.Sql("ALTER TABLE manifests ALTER COLUMN label TYPE jsonb USING label::jsonb;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE manifests ALTER COLUMN label TYPE text USING label::text;");
        }
    }
}
