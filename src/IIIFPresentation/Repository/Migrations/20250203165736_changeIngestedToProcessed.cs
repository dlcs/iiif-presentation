using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class changeIngestedToProcessed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ingested",
                table: "manifests");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_processed",
                table: "manifests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_processed",
                table: "manifests");

            migrationBuilder.AddColumn<bool>(
                name: "ingested",
                table: "manifests",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
