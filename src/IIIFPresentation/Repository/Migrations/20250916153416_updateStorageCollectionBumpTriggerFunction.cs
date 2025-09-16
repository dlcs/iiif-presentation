using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class updateStorageCollectionBumpTriggerFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                create or replace function storage_collection_bump()
                     returns trigger as
                $$
                BEGIN
                    DECLARE
                        newHierarchy hierarchy%ROWTYPE; oldHierarchy hierarchy%ROWTYPE; affected_ids TEXT[]; cus_id integer;
                    BEGIN
                        IF TG_OP = 'UPDATE' THEN
                            oldHierarchy := OLD;
                            newHierarchy := NEW;
                
                            IF newHierarchy.parent IS NOT NULL AND (oldHierarchy.parent <> newHierarchy.parent OR oldHierarchy.slug <> newHierarchy.slug) THEN
                                affected_ids := ARRAY[
                                    oldHierarchy.parent,
                                    newHierarchy.parent
                                    ];
                                cus_id := oldHierarchy.customer_id;
                            END IF;
                
                        ELSIF TG_OP = 'INSERT' THEN
                            newHierarchy := NEW;
                
                            IF newHierarchy.parent IS NOT NULL THEN
                                affected_ids := ARRAY[newHierarchy.parent];
                                cus_id := newHierarchy.customer_id;
                            END IF;
                
                        ELSIF TG_OP = 'DELETE' THEN
                            oldHierarchy := OLD;
                
                            IF oldHierarchy.parent IS NOT NULL THEN
                                affected_ids := ARRAY[oldHierarchy.parent];
                                cus_id := oldHierarchy.customer_id;
                            END IF;
                        END IF;
                
                        IF affected_ids IS NOT NULL THEN
                            UPDATE collections
                            SET modified = now()
                            WHERE id = ANY (affected_ids)
                            AND customer_id = cus_id;
                        END IF;
                
                        RETURN NULL; 
                    END;
                END;
                $$ LANGUAGE plpgsql;
                """
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                
                            IF newHierarchy.parent IS NOT NULL AND (oldHierarchy.parent <> newHierarchy.parent OR oldHierarchy.slug <> newHierarchy.slug) THEN
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
        }
    }
}
