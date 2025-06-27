using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class addStorageCollectionHierarchyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION storage_collection_bump()
                    RETURNS TRIGGER AS
                $$
                BEGIN
                    DECLARE
                        newHierarchy hierarchy%ROWTYPE; oldHierarchy hierarchy%ROWTYPE; affected_ids TEXT[];
                    BEGIN
                        IF TG_OP = 'UPDATE' THEN
                            oldHierarchy := OLD;
                            newHierarchy := NEW;
                
                            IF newHierarchy.parent IS NOT NULL AND oldHierarchy.parent <> newHierarchy.parent THEN
                                affected_ids := ARRAY[
                                    oldHierarchy.parent,
                                    newHierarchy.parent
                                    ];
                            END IF;
                
                        ELSIF TG_OP = 'INSERT' THEN
                            newHierarchy := NEW;
                
                            IF newHierarchy.parent IS NOT NULL THEN
                                affected_ids := ARRAY[newHierarchy.parent];
                            END IF;
                
                        ELSIF TG_OP = 'DELETE' THEN
                            oldHierarchy := OLD;
                
                            IF oldHierarchy.parent IS NOT NULL THEN
                                affected_ids := ARRAY[oldHierarchy.parent];
                            END IF;
                        END IF;
                
                        IF affected_ids IS NOT NULL THEN
                            UPDATE collections
                            SET modified = now()
                            WHERE id = ANY (affected_ids);
                        END IF;
                
                        RETURN NULL; 
                    END;
                END;
                $$ LANGUAGE plpgsql;
                """
                );

            migrationBuilder.Sql(
                """
                CREATE TRIGGER storage_collection_bump_trigger
                    AFTER INSERT OR UPDATE OR DELETE ON hierarchy
                    FOR EACH ROW
                EXECUTE FUNCTION storage_collection_bump();
                """
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS storage_collection_bump_trigger ON hierarchy;");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS storage_collection_bump();");
        }
    }
}
