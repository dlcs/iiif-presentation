using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class Hierarchyparentfk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_collections_customer_id_id",
                table: "collections",
                columns: new[] { "customer_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_customer_id_parent",
                table: "hierarchy",
                columns: new[] { "customer_id", "parent" });

            migrationBuilder.AddForeignKey(
                name: "fk_hierarchy_collections_customer_id_parent",
                table: "hierarchy",
                columns: new[] { "customer_id", "parent" },
                principalTable: "collections",
                principalColumns: new[] { "customer_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_hierarchy_collections_customer_id_parent",
                table: "hierarchy");

            migrationBuilder.DropIndex(
                name: "ix_hierarchy_customer_id_parent",
                table: "hierarchy");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_collections_customer_id_id",
                table: "collections");
        }
    }
}
