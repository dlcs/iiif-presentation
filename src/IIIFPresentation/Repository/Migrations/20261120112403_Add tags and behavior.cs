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
                nullable: false,
                defaultValueSql: "'{}'");

            // existing manifests have no tags, so they get an empty array. The default only fills existing rows, and is
            // dropped so the column has none
            migrationBuilder.Sql("""
                ALTER TABLE manifests ADD tags text[] DEFAULT '{}';
                ALTER TABLE manifests ALTER COLUMN tags DROP DEFAULT;
                """);

            migrationBuilder.AddColumn<List<string>>(
                name: "behavior",
                table: "collections",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            // is_public and is_storage_collection are replaced by entries in behavior. Rows with neither flag keep
            // the column default
            migrationBuilder.Sql("""
                UPDATE collections
                SET behavior = array_remove(ARRAY[
                    CASE WHEN is_public THEN 'public-iiif' END,
                    CASE WHEN is_storage_collection THEN 'storage-collection' END
                ], NULL)
                WHERE is_public OR is_storage_collection;
                """);

            migrationBuilder.DropColumn(
                name: "is_public",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "is_storage_collection",
                table: "collections");

            // comma-separated tags become one entry per tag, trimmed, with empty entries removed. Collections with no
            // tags get an empty array
            migrationBuilder.Sql("""
                ALTER TABLE collections ALTER COLUMN tags TYPE text[]
                USING COALESCE(array_remove(regexp_split_to_array(btrim(tags), '\s*,\s*'), ''), '{}');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE collections ALTER COLUMN tags TYPE text
                USING NULLIF(array_to_string(tags, ', '), '');
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
                SET is_public = 'public-iiif' = ANY(behavior),
                    is_storage_collection = 'storage-collection' = ANY(behavior)
                WHERE cardinality(behavior) > 0;
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
