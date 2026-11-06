using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddWarningAndResultToPipelineJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "result",
                table: "pipeline_jobs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "warning",
                table: "pipeline_jobs",
                type: "text",
                nullable: true);

            // Existing CompletedNoOperation jobs are all text jobs, which reported this warning
            migrationBuilder.Sql("""
                UPDATE pipeline_jobs
                SET warning = 'No text resources found'
                WHERE status = 'CompletedNoOperation' AND job_type = 'TextService';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "result",
                table: "pipeline_jobs");

            migrationBuilder.DropColumn(
                name: "warning",
                table: "pipeline_jobs");
        }
    }
}
