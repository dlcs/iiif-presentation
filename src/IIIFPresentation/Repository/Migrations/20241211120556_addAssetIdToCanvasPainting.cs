using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class addAssetIdToCanvasPainting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_canvas_o",
                table: "canvas_paintings");

            migrationBuilder.AddColumn<string>(
                name: "asset_id",
                table: "canvas_paintings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_asset_id",
                table: "canvas_paintings",
                columns: new[] { "canvas_id", "customer_id", "manifest_id", "asset_id", "canvas_order", "choice_order" },
                unique: true,
                filter: "canvas_original_id is null");

            migrationBuilder.CreateIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_canvas_o",
                table: "canvas_paintings",
                columns: new[] { "canvas_id", "customer_id", "manifest_id", "canvas_original_id", "canvas_order", "choice_order" },
                unique: true,
                filter: "asset_id is null");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_asset_id",
                table: "canvas_paintings");

            migrationBuilder.DropIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_canvas_o",
                table: "canvas_paintings");

            migrationBuilder.DropColumn(
                name: "asset_id",
                table: "canvas_paintings");

            migrationBuilder.CreateIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_canvas_o",
                table: "canvas_paintings",
                columns: new[] { "canvas_id", "customer_id", "manifest_id", "canvas_original_id", "canvas_order", "choice_order" },
                unique: true);
        }
    }
}
