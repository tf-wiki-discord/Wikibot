﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wikibot.App.Models.Jobs;

namespace Wikibot.App.Migrations
{
    [DbContext(typeof(JobContext))]
    [Migration("20200715203839_Wikibot.App.Models.Jobs.JobContext2")]
    partial class WikibotAppModelsJobsJobContext2
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Wikibot.App.Jobs.Page", b =>
                {
                    b.Property<long>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<long?>("TextReplacementJobID")
                        .HasColumnType("bigint");

                    b.HasKey("ID");

                    b.HasIndex("TextReplacementJobID");

                    b.ToTable("Page");
                });

            modelBuilder.Entity("Wikibot.App.Jobs.WikiJob", b =>
                {
                    b.Property<long>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Comment")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Discriminator")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Notes")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ProposedChanges")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RawRequest")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("RequestType")
                        .HasColumnType("int");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.Property<DateTime>("SubmittedDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("UserName")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ID");

                    b.ToTable("Jobs");

                    b.HasDiscriminator<string>("Discriminator").HasValue("WikiJob");
                });

            modelBuilder.Entity("Wikibot.App.Jobs.TextReplacementJob", b =>
                {
                    b.HasBaseType("Wikibot.App.Jobs.WikiJob");

                    b.Property<string>("FromText")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ToText")
                        .HasColumnType("nvarchar(max)");

                    b.HasDiscriminator().HasValue("TextReplacementJob");
                });

            modelBuilder.Entity("Wikibot.App.Jobs.Page", b =>
                {
                    b.HasOne("Wikibot.App.Jobs.TextReplacementJob", null)
                        .WithMany("PageNames")
                        .HasForeignKey("TextReplacementJobID");
                });
#pragma warning restore 612, 618
        }
    }
}
