using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class addHierarchyTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hierarchy",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resource_id = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    parent = table.Column<string>(type: "text", nullable: true),
                    items_order = table.Column<int>(type: "integer", nullable: true),
                    @public = table.Column<bool>(name: "public", type: "boolean", nullable: false),
                    canonical = table.Column<bool>(type: "boolean", nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hierarchy", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "manifests",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manifests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_slug_parent_customer_id",
                table: "hierarchy",
                columns: new[] { "slug", "parent", "customer_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hierarchy");

            migrationBuilder.DropTable(
                name: "manifests");
        }
    }
}
