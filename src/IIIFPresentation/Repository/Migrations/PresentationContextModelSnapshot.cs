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

                    b.Property<int?>("ItemsOrder")
                        .HasColumnType("integer")
                        .HasColumnName("items_order");

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

                    b.Property<string>("Parent")
                        .HasColumnType("text")
                        .HasColumnName("parent");

                    b.Property<string>("Slug")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("slug");

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

                    b.HasIndex("CustomerId", "Slug", "Parent")
                        .IsUnique()
                        .HasDatabaseName("ix_collections_customer_id_slug_parent");

                    b.ToTable("collections", (string)null);
                });

            modelBuilder.Entity("Models.Database.Collections.Manifest", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.HasKey("Id")
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

                    b.Property<int>("CustomerId")
                        .HasColumnType("integer")
                        .HasColumnName("customer_id");

                    b.Property<int?>("ItemsOrder")
                        .HasColumnType("integer")
                        .HasColumnName("items_order");

                    b.Property<string>("Parent")
                        .HasColumnType("text")
                        .HasColumnName("parent");

                    b.Property<bool>("Public")
                        .HasColumnType("boolean")
                        .HasColumnName("public");

                    b.Property<string>("ResourceId")
                        .HasColumnType("text")
                        .HasColumnName("resource_id");

                    b.Property<string>("Slug")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("slug");

                    b.Property<int>("Type")
                        .HasColumnType("integer")
                        .HasColumnName("type");

                    b.HasKey("Id")
                        .HasName("pk_hierarchy");

                    b.HasIndex("Slug", "Parent", "CustomerId")
                        .HasDatabaseName("ix_hierarchy_slug_parent_customer_id");

                    b.ToTable("hierarchy", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}
