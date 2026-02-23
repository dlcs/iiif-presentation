using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class Removepgcrypto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,");

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
                      ELSE encode(substr(sha256(ts::text::bytea), 1, 16), 'hex')::uuid
                    END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");
        }
    }
}
