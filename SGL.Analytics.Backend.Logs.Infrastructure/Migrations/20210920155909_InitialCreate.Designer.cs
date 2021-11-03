﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	[DbContext(typeof(LogsContext))]
	[Migration("20210920155909_InitialCreate")]
	partial class InitialCreate {
		/// <inheritdoc/>
		protected override void BuildTargetModel(ModelBuilder modelBuilder) {
#pragma warning disable 612, 618
			modelBuilder
				.HasAnnotation("Relational:MaxIdentifierLength", 63)
				.HasAnnotation("ProductVersion", "5.0.9")
				.HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.Application", b => {
				b.Property<Guid>("Id")
					.ValueGeneratedOnAdd()
					.HasColumnType("uuid");

				b.Property<string>("ApiToken")
					.IsRequired()
					.HasMaxLength(64)
					.HasColumnType("character varying(64)");

				b.Property<string>("Name")
					.IsRequired()
					.HasMaxLength(128)
					.HasColumnType("character varying(128)");

				b.HasKey("Id");

				b.HasIndex("Name")
					.IsUnique();

				b.ToTable("Applications");
			});

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.LogMetadata", b => {
				b.Property<Guid>("Id")
					.ValueGeneratedOnAdd()
					.HasColumnType("uuid");

				b.Property<Guid>("AppId")
					.HasColumnType("uuid");

				b.Property<bool>("Complete")
					.HasColumnType("boolean");

				b.Property<DateTime>("CreationTime")
					.HasColumnType("timestamp without time zone");

				b.Property<DateTime>("EndTime")
					.HasColumnType("timestamp without time zone");

				b.Property<string>("FilenameSuffix")
					.IsRequired()
					.HasMaxLength(16)
					.HasColumnType("character varying(16)");

				b.Property<Guid>("LocalLogId")
					.HasColumnType("uuid");

				b.Property<DateTime>("UploadTime")
					.HasColumnType("timestamp without time zone");

				b.Property<Guid>("UserId")
					.HasColumnType("uuid");

				b.HasKey("Id");

				b.HasIndex("AppId");

				b.HasIndex("AppId", "UserId");

				b.ToTable("LogMetadata");
			});

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.LogMetadata", b => {
				b.HasOne("SGL.Analytics.Backend.Domain.Entity.Application", "App")
					.WithMany()
					.HasForeignKey("AppId")
					.OnDelete(DeleteBehavior.Cascade)
					.IsRequired();

				b.Navigation("App");
			});
#pragma warning restore 612, 618
		}
	}
}
