using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class hierarchychoice_ordernullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "choice_order",
                table: "canvas_paintings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
            
            migrationBuilder.Sql("UPDATE canvas_paintings SET choice_order = NULL WHERE choice_order = -1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "choice_order",
                table: "canvas_paintings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
