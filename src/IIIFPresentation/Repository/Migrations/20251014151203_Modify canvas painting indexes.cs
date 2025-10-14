using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class Modifycanvaspaintingindexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_canvas_o1",
                table: "canvas_paintings",
                columns: new[] { "canvas_id", "customer_id", "manifest_id", "canvas_original_id", "asset_id", "canvas_order", "choice_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_canvas_o1",
                table: "canvas_paintings");
        }
    }
}
