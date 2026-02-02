using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Credo.BlobStorage.Migrator.Data.Migration.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Microsoft.EntityFrameworkCore.Migrations.Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Ensure schema exists
        migrationBuilder.EnsureSchema(name: "migration");

        migrationBuilder.CreateTable(
            name: "MigrationLog",
            schema: "migration",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SourceDocumentId = table.Column<long>(type: "bigint", nullable: false),
                SourceYear = table.Column<int>(type: "int", nullable: false),
                OriginalFilename = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                OriginalExtension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                ClaimedContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SourceFileSize = table.Column<int>(type: "int", nullable: false),
                SourceRecordDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                TargetDocId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                TargetBucket = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: true),
                TargetFilename = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                TargetSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                DetectedContentType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MigrationLog", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MigrationLog_Status",
            schema: "migration",
            table: "MigrationLog",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "UQ_MigrationLog_SourceYear_SourceDocumentId",
            schema: "migration",
            table: "MigrationLog",
            columns: new[] { "SourceYear", "SourceDocumentId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MigrationLog",
            schema: "migration");
    }
}
