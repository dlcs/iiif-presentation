using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class addEtagManifestCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.Sql(
"""
CREATE OR REPLACE FUNCTION deterministic_uuid_sha256(ts timestamptz, txt text)
RETURNS uuid
LANGUAGE sql
IMMUTABLE
AS $$
  SELECT
    CASE
      WHEN ts IS NULL THEN '00000000-0000-0000-0000-000000000000'::uuid
      ELSE encode(substr(digest(ts::text || txt, 'sha256'), 1, 16), 'hex')::uuid
    END
$$;
"""
            );

            migrationBuilder.AddColumn<Guid>(
                name: "etag",
                table: "manifests",
                type: "uuid",
                nullable: false,
                computedColumnSql: "deterministic_uuid_sha256(\"last_processed\", \"id\")",
                stored: true);

            migrationBuilder.AddColumn<Guid>(
                name: "etag",
                table: "collections",
                type: "uuid",
                nullable: false,
                computedColumnSql: "deterministic_uuid_sha256(\"modified\", \"id\")",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "etag",
                table: "manifests");

            migrationBuilder.DropColumn(
                name: "etag",
                table: "collections");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS deterministic_uuid_sha256(timestamptz, text);");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,");
        }
    }
}
