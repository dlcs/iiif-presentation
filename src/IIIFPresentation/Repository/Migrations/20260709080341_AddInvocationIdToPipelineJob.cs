using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddInvocationIdToPipelineJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "invocation_id",
                table: "pipeline_jobs",
                type: "text",
                nullable: true);

            // Give pre-existing rows that represent a real past invocation (i.e. not NotSubmitted/FailedToSubmit,
            // which never got a real id and correctly stay null) sequential invocation ids per resource+type,
            // ordered by when they were created - otherwise two historical jobs for the same resource would tie
            // on invocation_id, making completion-notification correlation ambiguous, and the unique index below
            // would fail to apply.
            migrationBuilder.Sql("""
                UPDATE pipeline_jobs AS pj
                SET invocation_id = ranked.row_num::text
                FROM (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY customer_id, COALESCE(manifest_id, collection_id), job_type
                               ORDER BY created
                           ) AS row_num
                    FROM pipeline_jobs
                    WHERE status NOT IN ('NotSubmitted', 'FailedToSubmit')
                ) AS ranked
                WHERE pj.id = ranked.id;
                """);

            // Excludes rows with a null invocation_id (NotSubmitted/FailedToSubmit, which never got a real value
            // from the pipeline service) - Postgres already treats multiple NULLs as non-colliding in a unique
            // index, so this reads directly off the data rather than naming specific statuses, and doesn't need
            // updating if new pre-submission statuses are added later.
            migrationBuilder.CreateIndex(
                name: "ix_pipeline_jobs_customer_id_manifest_id_collection_id_job_typ",
                table: "pipeline_jobs",
                columns: new[] { "customer_id", "manifest_id", "collection_id", "job_type", "invocation_id" },
                unique: true,
                filter: "invocation_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pipeline_jobs_customer_id_manifest_id_collection_id_job_typ",
                table: "pipeline_jobs");

            migrationBuilder.DropColumn(
                name: "invocation_id",
                table: "pipeline_jobs");
        }
    }
}
