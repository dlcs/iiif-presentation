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
            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_parent_customer_id",
                table: "hierarchy",
                columns: new[] { "parent", "customer_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_hierarchy_collections_parent_customer_id",
                table: "hierarchy",
                columns: new[] { "parent", "customer_id" },
                principalTable: "collections",
                principalColumns: new[] { "id", "customer_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_hierarchy_collections_parent_customer_id",
                table: "hierarchy");

            migrationBuilder.DropIndex(
                name: "ix_hierarchy_parent_customer_id",
                table: "hierarchy");
        }
    }
}
