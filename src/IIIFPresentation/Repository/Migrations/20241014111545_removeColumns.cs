using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class removeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manifests");

            migrationBuilder.DropIndex(
                name: "ix_hierarchy_slug_parent_customer_id",
                table: "hierarchy");

            migrationBuilder.DropIndex(
                name: "ix_collections_customer_id_slug_parent",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "items_order",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "parent",
                table: "collections");

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_customer_id_slug_parent",
                table: "hierarchy",
                columns: new[] { "customer_id", "slug", "parent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_resource_id_customer_id_canonical_type",
                table: "hierarchy",
                columns: new[] { "resource_id", "customer_id", "canonical", "type" },
                unique: true,
                filter: "canonical is true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hierarchy_customer_id_slug_parent",
                table: "hierarchy");

            migrationBuilder.DropIndex(
                name: "ix_hierarchy_resource_id_customer_id_canonical_type",
                table: "hierarchy");

            migrationBuilder.AddColumn<int>(
                name: "items_order",
                table: "collections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parent",
                table: "collections",
                type: "text",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_collections_customer_id_slug_parent",
                table: "collections",
                columns: new[] { "customer_id", "slug", "parent" },
                unique: true);
        }
    }
}
