using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class addingNewPk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_collections",
                table: "collections");

            migrationBuilder.AddPrimaryKey(
                name: "pk_collections",
                table: "collections",
                columns: new[] { "id", "customer_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_collections",
                table: "collections");

            migrationBuilder.AddPrimaryKey(
                name: "pk_collections",
                table: "collections",
                column: "id");
        }
    }
}
