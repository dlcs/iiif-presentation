using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class renameBatchFinishedAndAddIngesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "finished",
                table: "batches",
                newName: "processed");

            migrationBuilder.AddColumn<bool>(
                name: "ingesting",
                table: "canvas_paintings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ingesting",
                table: "canvas_paintings");

            migrationBuilder.RenameColumn(
                name: "processed",
                table: "batches",
                newName: "finished");
        }
    }
}
