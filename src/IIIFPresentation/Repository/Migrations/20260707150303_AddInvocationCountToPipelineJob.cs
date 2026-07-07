using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddInvocationCountToPipelineJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "invocation_count",
                table: "pipeline_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Give pre-existing rows sequential invocation counts per resource+type, ordered by when they
            // were created, instead of leaving every row at the flat default of 1 - otherwise two historical
            // jobs for the same resource would tie on invocation_count, making completion-notification
            // correlation ambiguous, and the unique index below would fail to apply.
            migrationBuilder.Sql("""
                UPDATE pipeline_jobs AS pj
                SET invocation_count = ranked.row_num
                FROM (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY customer_id, COALESCE(manifest_id, collection_id), job_type
                               ORDER BY created
                           ) AS row_num
                    FROM pipeline_jobs
                ) AS ranked
                WHERE pj.id = ranked.id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_jobs_customer_id_manifest_id_collection_id_job_typ",
                table: "pipeline_jobs",
                columns: new[] { "customer_id", "manifest_id", "collection_id", "job_type", "invocation_count" },
                unique: true,
                filter: "status NOT IN ('NotSubmitted', 'FailedToSubmit')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pipeline_jobs_customer_id_manifest_id_collection_id_job_typ",
                table: "pipeline_jobs");

            migrationBuilder.DropColumn(
                name: "invocation_count",
                table: "pipeline_jobs");
        }
    }
}
