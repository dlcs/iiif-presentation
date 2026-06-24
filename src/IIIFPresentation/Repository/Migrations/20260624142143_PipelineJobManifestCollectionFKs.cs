using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class PipelineJobManifestCollectionFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "resource_id",
                table: "pipeline_jobs");

            migrationBuilder.DropColumn(
                name: "resource_type",
                table: "pipeline_jobs");

            migrationBuilder.AddColumn<string>(
                name: "collection_id",
                table: "pipeline_jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manifest_id",
                table: "pipeline_jobs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_jobs_collection_id_customer_id",
                table: "pipeline_jobs",
                columns: new[] { "collection_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_jobs_manifest_id_customer_id",
                table: "pipeline_jobs",
                columns: new[] { "manifest_id", "customer_id" });

            migrationBuilder.AddCheckConstraint(
                name: "stop_collection_and_manifest_in_same_record",
                table: "pipeline_jobs",
                sql: "num_nonnulls(manifest_id, collection_id) = 1");

            migrationBuilder.AddForeignKey(
                name: "fk_pipeline_jobs_collections_collection_id_customer_id",
                table: "pipeline_jobs",
                columns: new[] { "collection_id", "customer_id" },
                principalTable: "collections",
                principalColumns: new[] { "id", "customer_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_pipeline_jobs_manifests_manifest_id_customer_id",
                table: "pipeline_jobs",
                columns: new[] { "manifest_id", "customer_id" },
                principalTable: "manifests",
                principalColumns: new[] { "id", "customer_id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pipeline_jobs_collections_collection_id_customer_id",
                table: "pipeline_jobs");

            migrationBuilder.DropForeignKey(
                name: "fk_pipeline_jobs_manifests_manifest_id_customer_id",
                table: "pipeline_jobs");

            migrationBuilder.DropIndex(
                name: "ix_pipeline_jobs_collection_id_customer_id",
                table: "pipeline_jobs");

            migrationBuilder.DropIndex(
                name: "ix_pipeline_jobs_manifest_id_customer_id",
                table: "pipeline_jobs");

            migrationBuilder.DropCheckConstraint(
                name: "stop_collection_and_manifest_in_same_record",
                table: "pipeline_jobs");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "pipeline_jobs");

            migrationBuilder.DropColumn(
                name: "manifest_id",
                table: "pipeline_jobs");

            migrationBuilder.AddColumn<string>(
                name: "resource_id",
                table: "pipeline_jobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "resource_type",
                table: "pipeline_jobs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
