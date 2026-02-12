using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Credo.BlobStorage.Migrator.Data.Migration.Migrations;

/// <inheritdoc />
public partial class AddContentIdDocumentId : Microsoft.EntityFrameworkCore.Migrations.Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ContentId",
            schema: "migration",
            table: "MigrationLog",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "DocumentId",
            schema: "migration",
            table: "MigrationLog",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_MigrationLog_SourceYear_ContentId",
            schema: "migration",
            table: "MigrationLog",
            columns: new[] { "SourceYear", "ContentId" });

        migrationBuilder.CreateIndex(
            name: "IX_MigrationLog_SourceYear_DocumentId",
            schema: "migration",
            table: "MigrationLog",
            columns: new[] { "SourceYear", "DocumentId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MigrationLog_SourceYear_DocumentId",
            schema: "migration",
            table: "MigrationLog");

        migrationBuilder.DropIndex(
            name: "IX_MigrationLog_SourceYear_ContentId",
            schema: "migration",
            table: "MigrationLog");

        migrationBuilder.DropColumn(
            name: "DocumentId",
            schema: "migration",
            table: "MigrationLog");

        migrationBuilder.DropColumn(
            name: "ContentId",
            schema: "migration",
            table: "MigrationLog");
    }
}
