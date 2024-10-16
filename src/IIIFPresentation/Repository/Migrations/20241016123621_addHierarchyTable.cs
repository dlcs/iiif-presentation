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
            migrationBuilder.DropIndex(
                name: "ix_collections_customer_id_slug_parent",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "items_order",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "parent",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "collections");

            migrationBuilder.CreateTable(
                name: "manifests",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manifests", x => new { x.id, x.customer_id });
                });

            migrationBuilder.CreateTable(
                name: "hierarchy",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<string>(type: "text", nullable: true),
                    manifest_id = table.Column<string>(type: "text", nullable: true),
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
                    table.CheckConstraint("stop_collection_and_manifest_in_same_record", "num_nonnulls(manifest_id, collection_id) = 1");
                    table.ForeignKey(
                        name: "fk_hierarchy_collections_collection_id_customer_id",
                        columns: x => new { x.collection_id, x.customer_id },
                        principalTable: "collections",
                        principalColumns: new[] { "id", "customer_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hierarchy_manifests_manifest_id_customer_id",
                        columns: x => new { x.manifest_id, x.customer_id },
                        principalTable: "manifests",
                        principalColumns: new[] { "id", "customer_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_collection_id_customer_id_canonical",
                table: "hierarchy",
                columns: new[] { "collection_id", "customer_id", "canonical" },
                unique: true,
                filter: "canonical is true");

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_customer_id_slug_parent",
                table: "hierarchy",
                columns: new[] { "customer_id", "slug", "parent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_manifest_id_customer_id_canonical",
                table: "hierarchy",
                columns: new[] { "manifest_id", "customer_id", "canonical" },
                unique: true,
                filter: "canonical is true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hierarchy");

            migrationBuilder.DropTable(
                name: "manifests");

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

            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "collections",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_collections_customer_id_slug_parent",
                table: "collections",
                columns: new[] { "customer_id", "slug", "parent" },
                unique: true);
        }
    }
}
