using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable enable

namespace Credo.BlobStorage.Migrator.Data.Migration.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Microsoft.EntityFrameworkCore.Migrations.Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MigrationLog",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                SourceDocumentId = table.Column<long>(type: "bigint", nullable: false),
                SourceYear = table.Column<int>(type: "integer", nullable: false),
                OriginalFilename = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                OriginalExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                ClaimedContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                SourceFileSize = table.Column<int>(type: "integer", nullable: false),
                SourceRecordDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                TargetDocId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                TargetBucket = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                TargetFilename = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                TargetSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                DetectedContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MigrationLog", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MigrationLog_Status",
            table: "MigrationLog",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "UQ_MigrationLog_SourceYear_SourceDocumentId",
            table: "MigrationLog",
            columns: new[] { "SourceYear", "SourceDocumentId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MigrationLog");
    }
}
