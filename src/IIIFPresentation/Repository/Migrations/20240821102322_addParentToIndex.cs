using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class addParentToIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_collections_customer_id_slug",
                table: "collections");

            migrationBuilder.CreateIndex(
                name: "ix_collections_customer_id_slug_parent",
                table: "collections",
                columns: new[] { "customer_id", "slug", "parent" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_collections_customer_id_slug_parent",
                table: "collections");

            migrationBuilder.CreateIndex(
                name: "ix_collections_customer_id_slug",
                table: "collections",
                columns: new[] { "customer_id", "slug" },
                unique: true);
        }
    }
}
