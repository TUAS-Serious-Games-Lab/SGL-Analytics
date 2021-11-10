﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SGL.Analytics.Backend.Users.Infrastructure.Data;

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	[DbContext(typeof(UsersContext))]
	[Migration("20211110153215_AddStringLengthConstraints")]
	partial class AddStringLengthConstraints {
		/// <inheritdoc/>
		protected override void BuildTargetModel(ModelBuilder modelBuilder) {
#pragma warning disable 612, 618
			modelBuilder
				.HasAnnotation("Relational:MaxIdentifierLength", 63)
				.HasAnnotation("ProductVersion", "5.0.10")
				.HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationUserPropertyDefinition", b => {
				b.Property<int>("Id")
					.ValueGeneratedOnAdd()
					.HasColumnType("integer")
					.HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

				b.Property<Guid>("AppId")
					.HasColumnType("uuid");

				b.Property<string>("Name")
					.IsRequired()
					.HasMaxLength(128)
					.HasColumnType("character varying(128)");

				b.Property<bool>("Required")
					.HasColumnType("boolean");

				b.Property<int>("Type")
					.HasColumnType("integer");

				b.HasKey("Id");

				b.HasIndex("AppId", "Name")
					.IsUnique();

				b.ToTable("ApplicationUserPropertyDefinitions");
			});

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", b => {
				b.Property<Guid>("Id")
					.ValueGeneratedOnAdd()
					.HasColumnType("uuid");

				b.Property<string>("ApiToken")
					.IsRequired()
					.HasMaxLength(50)
					.HasColumnType("character varying(50)");

				b.Property<string>("Name")
					.IsRequired()
					.HasMaxLength(128)
					.HasColumnType("character varying(128)");

				b.HasKey("Id");

				b.HasIndex("Name")
					.IsUnique();

				b.ToTable("Applications");
			});

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.UserRegistration", b => {
				b.Property<Guid>("Id")
					.ValueGeneratedOnAdd()
					.HasColumnType("uuid");

				b.Property<Guid>("AppId")
					.HasColumnType("uuid");

				b.Property<string>("HashedSecret")
					.IsRequired()
					.HasMaxLength(128)
					.HasColumnType("character varying(128)");

				b.Property<string>("Username")
					.IsRequired()
					.HasMaxLength(64)
					.HasColumnType("character varying(64)");

				b.HasKey("Id");

				b.HasIndex("AppId", "Username")
					.IsUnique();

				b.ToTable("UserRegistrations");
			});

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationUserPropertyDefinition", b => {
				b.HasOne("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", "App")
					.WithMany("UserProperties")
					.HasForeignKey("AppId")
					.OnDelete(DeleteBehavior.Cascade)
					.IsRequired();

				b.Navigation("App");
			});

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.UserRegistration", b => {
				b.HasOne("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", "App")
					.WithMany("UserRegistrations")
					.HasForeignKey("AppId")
					.OnDelete(DeleteBehavior.Cascade)
					.IsRequired();

				b.OwnsMany("SGL.Analytics.Backend.Domain.Entity.ApplicationUserPropertyInstance", "AppSpecificProperties", b1 => {
					b1.Property<int>("Id")
						.ValueGeneratedOnAdd()
						.HasColumnType("integer")
						.HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

					b1.Property<DateTime?>("DateTimeValue")
						.HasColumnType("timestamp without time zone");

					b1.Property<int>("DefinitionId")
						.HasColumnType("integer");

					b1.Property<double?>("FloatingPointValue")
						.HasColumnType("double precision");

					b1.Property<Guid?>("GuidValue")
						.HasColumnType("uuid");

					b1.Property<int?>("IntegerValue")
						.HasColumnType("integer");

					b1.Property<string>("JsonValue")
						.HasColumnType("text");

					b1.Property<string>("StringValue")
						.HasColumnType("text");

					b1.Property<Guid>("UserId")
						.HasColumnType("uuid");

					b1.HasKey("Id");

					b1.HasIndex("UserId");

					b1.HasIndex("DefinitionId", "UserId")
						.IsUnique();

					b1.ToTable("ApplicationUserPropertyInstances");

					b1.HasOne("SGL.Analytics.Backend.Domain.Entity.ApplicationUserPropertyDefinition", "Definition")
						.WithMany()
						.HasForeignKey("DefinitionId")
						.OnDelete(DeleteBehavior.Cascade)
						.IsRequired();

					b1.WithOwner("User")
						.HasForeignKey("UserId");

					b1.Navigation("Definition");

					b1.Navigation("User");
				});

				b.Navigation("App");

				b.Navigation("AppSpecificProperties");
			});

			modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", b => {
				b.Navigation("UserProperties");

				b.Navigation("UserRegistrations");
			});
#pragma warning restore 612, 618
		}
	}
}
