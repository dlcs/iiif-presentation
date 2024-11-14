﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Repository;

#nullable disable

namespace Repository.Migrations
{
    [DbContext(typeof(PresentationContext))]
    partial class PresentationContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Models.Database.Collections.Collection", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<int>("CustomerId")
                        .HasColumnType("integer")
                        .HasColumnName("customer_id");

                    b.Property<DateTime>("Created")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created");

                    b.Property<string>("CreatedBy")
                        .HasColumnType("text")
                        .HasColumnName("created_by");

                    b.Property<bool>("IsPublic")
                        .HasColumnType("boolean")
                        .HasColumnName("is_public");

                    b.Property<bool>("IsStorageCollection")
                        .HasColumnType("boolean")
                        .HasColumnName("is_storage_collection");

                    b.Property<string>("Label")
                        .HasColumnType("jsonb")
                        .HasColumnName("label");

                    b.Property<string>("LockedBy")
                        .HasColumnType("text")
                        .HasColumnName("locked_by");

                    b.Property<DateTime>("Modified")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("modified");

                    b.Property<string>("ModifiedBy")
                        .HasColumnType("text")
                        .HasColumnName("modified_by");

                    b.Property<string>("Tags")
                        .HasColumnType("text")
                        .HasColumnName("tags");

                    b.Property<string>("Thumbnail")
                        .HasColumnType("text")
                        .HasColumnName("thumbnail");

                    b.Property<bool>("UsePath")
                        .HasColumnType("boolean")
                        .HasColumnName("use_path");

                    b.HasKey("Id", "CustomerId")
                        .HasName("pk_collections");

                    b.ToTable("collections", (string)null);
                });

            modelBuilder.Entity("Models.Database.Collections.Manifest", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<int>("CustomerId")
                        .HasColumnType("integer")
                        .HasColumnName("customer_id");

                    b.Property<DateTime>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created")
                        .HasDefaultValueSql("now()");

                    b.Property<string>("CreatedBy")
                        .HasColumnType("text")
                        .HasColumnName("created_by");

                    b.Property<string>("Label")
                        .HasColumnType("text")
                        .HasColumnName("label");

                    b.Property<DateTime>("Modified")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("modified")
                        .HasDefaultValueSql("now()");

                    b.Property<string>("ModifiedBy")
                        .HasColumnType("text")
                        .HasColumnName("modified_by");

                    b.HasKey("Id", "CustomerId")
                        .HasName("pk_manifests");

                    b.ToTable("manifests", (string)null);
                });

            modelBuilder.Entity("Models.Database.General.Hierarchy", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<bool>("Canonical")
                        .HasColumnType("boolean")
                        .HasColumnName("canonical");

                    b.Property<string>("CollectionId")
                        .HasColumnType("text")
                        .HasColumnName("collection_id");

                    b.Property<int>("CustomerId")
                        .HasColumnType("integer")
                        .HasColumnName("customer_id");

                    b.Property<int?>("ItemsOrder")
                        .HasColumnType("integer")
                        .HasColumnName("items_order");

                    b.Property<string>("ManifestId")
                        .HasColumnType("text")
                        .HasColumnName("manifest_id");

                    b.Property<string>("Parent")
                        .HasColumnType("text")
                        .HasColumnName("parent");

                    b.Property<string>("Slug")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("slug");

                    b.Property<int>("Type")
                        .HasColumnType("integer")
                        .HasColumnName("type");

                    b.HasKey("Id")
                        .HasName("pk_hierarchy");

                    b.HasIndex("CollectionId", "CustomerId", "Canonical")
                        .IsUnique()
                        .HasDatabaseName("ix_hierarchy_collection_id_customer_id_canonical")
                        .HasFilter("canonical is true");

                    b.HasIndex("CustomerId", "Slug", "Parent")
                        .IsUnique()
                        .HasDatabaseName("ix_hierarchy_customer_id_slug_parent");

                    b.HasIndex("ManifestId", "CustomerId", "Canonical")
                        .IsUnique()
                        .HasDatabaseName("ix_hierarchy_manifest_id_customer_id_canonical")
                        .HasFilter("canonical is true");

                    b.ToTable("hierarchy", null, t =>
                        {
                            t.HasCheckConstraint("stop_collection_and_manifest_in_same_record", "num_nonnulls(manifest_id, collection_id) = 1");
                        });
                });

            modelBuilder.Entity("Models.Database.General.Hierarchy", b =>
                {
                    b.HasOne("Models.Database.Collections.Collection", "Collection")
                        .WithMany("Hierarchy")
                        .HasForeignKey("CollectionId", "CustomerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .HasConstraintName("fk_hierarchy_collections_collection_id_customer_id");

                    b.HasOne("Models.Database.Collections.Manifest", "Manifest")
                        .WithMany("Hierarchy")
                        .HasForeignKey("ManifestId", "CustomerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .HasConstraintName("fk_hierarchy_manifests_manifest_id_customer_id");

                    b.Navigation("Collection");

                    b.Navigation("Manifest");
                });

            modelBuilder.Entity("Models.Database.Collections.Collection", b =>
                {
                    b.Navigation("Hierarchy");
                });

            modelBuilder.Entity("Models.Database.Collections.Manifest", b =>
                {
                    b.Navigation("Hierarchy");
                });
#pragma warning restore 612, 618
        }
    }
}
