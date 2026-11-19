using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class Addtagsandbehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "behavior",
                table: "manifests",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "tags",
                table: "manifests",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "behavior",
                table: "collections",
                type: "text[]",
                nullable: true);

            // is_public and is_storage_collection are replaced by entries in behavior
            migrationBuilder.Sql("""
                UPDATE collections
                SET behavior = NULLIF(
                    array_remove(ARRAY[
                        CASE WHEN is_public THEN 'public-iiif' END,
                        CASE WHEN is_storage_collection THEN 'storage-collection' END
                    ], NULL),
                    '{}'::text[]);
                """);

            migrationBuilder.DropColumn(
                name: "is_public",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "is_storage_collection",
                table: "collections");

            // comma-separated tags become one entry per tag, trimmed, with empty entries removed
            migrationBuilder.Sql("""
                ALTER TABLE collections ALTER COLUMN tags TYPE text[]
                USING NULLIF(
                    array_remove(regexp_split_to_array(btrim(tags), '\s*,\s*'), ''),
                    '{}'::text[]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE collections ALTER COLUMN tags TYPE text
                USING array_to_string(tags, ', ');
                """);

            migrationBuilder.AddColumn<bool>(
                name: "is_public",
                table: "collections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_storage_collection",
                table: "collections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE collections
                SET is_public = COALESCE('public-iiif' = ANY(behavior), false),
                    is_storage_collection = COALESCE('storage-collection' = ANY(behavior), false);
                """);

            migrationBuilder.DropColumn(
                name: "behavior",
                table: "manifests");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "manifests");

            migrationBuilder.DropColumn(
                name: "behavior",
                table: "collections");
        }
    }
}
