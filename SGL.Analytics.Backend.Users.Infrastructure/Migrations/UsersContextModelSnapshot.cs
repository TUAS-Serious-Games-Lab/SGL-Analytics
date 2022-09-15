﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SGL.Analytics.Backend.Users.Infrastructure.Data;

#nullable disable

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations
{
    [DbContext(typeof(UsersContext))]
    partial class UsersContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationUserPropertyDefinition", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

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

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", b =>
                {
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

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ExporterKeyAuthCertificate", b =>
                {
                    b.Property<Guid>("AppId")
                        .HasColumnType("uuid");

                    b.Property<byte[]>("PublicKeyId")
                        .HasMaxLength(34)
                        .HasColumnType("bytea");

                    b.Property<string>("CertificatePem")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Label")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.HasKey("AppId", "PublicKeyId");

                    b.ToTable("ExporterKeyAuthCertificates");
                });

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.UserRegistration", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AppId")
                        .HasColumnType("uuid");

                    b.Property<byte[]>("EncryptedProperties")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<string>("HashedSecret")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<int>("PropertyEncryptionMode")
                        .HasColumnType("integer");

                    b.Property<byte[]>("PropertyInitializationVector")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<byte[]>("PropertySharedPublicKey")
                        .HasColumnType("bytea");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.HasKey("Id");

                    b.HasIndex("AppId", "Username")
                        .IsUnique();

                    b.ToTable("UserRegistrations");
                });

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationUserPropertyDefinition", b =>
                {
                    b.HasOne("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", "App")
                        .WithMany("UserProperties")
                        .HasForeignKey("AppId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("App");
                });

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", b =>
                {
                    b.OwnsMany("SGL.Analytics.Backend.Domain.Entity.Recipient", "DataRecipients", b1 =>
                        {
                            b1.Property<Guid>("AppId")
                                .HasColumnType("uuid");

                            b1.Property<byte[]>("PublicKeyId")
                                .HasMaxLength(34)
                                .HasColumnType("bytea");

                            b1.Property<string>("CertificatePem")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.Property<string>("Label")
                                .IsRequired()
                                .HasMaxLength(128)
                                .HasColumnType("character varying(128)");

                            b1.HasKey("AppId", "PublicKeyId");

                            b1.ToTable("Recipient");

                            b1.WithOwner("App")
                                .HasForeignKey("AppId");

                            b1.Navigation("App");
                        });

                    b.Navigation("DataRecipients");
                });

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ExporterKeyAuthCertificate", b =>
                {
                    b.HasOne("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", "App")
                        .WithMany("AuthorizedExporters")
                        .HasForeignKey("AppId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("App");
                });

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.UserRegistration", b =>
                {
                    b.HasOne("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", "App")
                        .WithMany("UserRegistrations")
                        .HasForeignKey("AppId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.OwnsMany("SGL.Analytics.Backend.Domain.Entity.ApplicationUserPropertyInstance", "AppSpecificProperties", b1 =>
                        {
                            b1.Property<int>("Id")
                                .ValueGeneratedOnAdd()
                                .HasColumnType("integer");

                            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b1.Property<int>("Id"));

                            b1.Property<DateTime?>("DateTimeValue")
                                .HasColumnType("timestamp with time zone");

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

                    b.OwnsMany("SGL.Analytics.Backend.Domain.Entity.UserRegistrationPropertyRecipientKey", "PropertyRecipientKeys", b1 =>
                        {
                            b1.Property<Guid>("UserId")
                                .HasColumnType("uuid");

                            b1.Property<byte[]>("RecipientKeyId")
                                .HasMaxLength(34)
                                .HasColumnType("bytea");

                            b1.Property<byte[]>("EncryptedKey")
                                .IsRequired()
                                .HasColumnType("bytea");

                            b1.Property<int>("EncryptionMode")
                                .HasColumnType("integer");

                            b1.Property<byte[]>("UserPropertiesPublicKey")
                                .HasColumnType("bytea");

                            b1.HasKey("UserId", "RecipientKeyId");

                            b1.ToTable("UserPropertyRecipientKeys", (string)null);

                            b1.WithOwner()
                                .HasForeignKey("UserId");
                        });

                    b.Navigation("App");

                    b.Navigation("AppSpecificProperties");

                    b.Navigation("PropertyRecipientKeys");
                });

            modelBuilder.Entity("SGL.Analytics.Backend.Domain.Entity.ApplicationWithUserProperties", b =>
                {
                    b.Navigation("AuthorizedExporters");

                    b.Navigation("UserProperties");

                    b.Navigation("UserRegistrations");
                });
#pragma warning restore 612, 618
        }
    }
}
