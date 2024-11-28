using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddcanvasPaintings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canvas_paintings",
                columns: table => new
                {
                    canvas_painting_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    canvas_id = table.Column<string>(type: "text", nullable: true),
                    manifest_id = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    canvas_original_id = table.Column<string>(type: "text", nullable: true),
                    canvas_order = table.Column<int>(type: "integer", nullable: false),
                    choice_order = table.Column<int>(type: "integer", nullable: false),
                    thumbnail = table.Column<string>(type: "text", nullable: true),
                    label = table.Column<string>(type: "jsonb", nullable: true),
                    canvas_label = table.Column<string>(type: "text", nullable: true),
                    target = table.Column<string>(type: "text", nullable: true),
                    static_width = table.Column<int>(type: "integer", nullable: true),
                    static_height = table.Column<int>(type: "integer", nullable: true),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_canvas_paintings", x => x.canvas_painting_id);
                    table.ForeignKey(
                        name: "fk_canvas_paintings_manifests_manifest_id_customer_id",
                        columns: x => new { x.manifest_id, x.customer_id },
                        principalTable: "manifests",
                        principalColumns: new[] { "id", "customer_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_canvas_paintings_canvas_id_customer_id_manifest_id_canvas_o",
                table: "canvas_paintings",
                columns: new[] { "canvas_id", "customer_id", "manifest_id", "canvas_original_id", "canvas_order", "choice_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_canvas_paintings_manifest_id_customer_id",
                table: "canvas_paintings",
                columns: new[] { "manifest_id", "customer_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canvas_paintings");
        }
    }
}
