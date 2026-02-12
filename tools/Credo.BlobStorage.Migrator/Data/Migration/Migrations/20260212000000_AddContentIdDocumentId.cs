using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Credo.BlobStorage.Migrator.Data.Migration.Migrations;

/// <inheritdoc />
public partial class AddContentIdDocumentId : Microsoft.EntityFrameworkCore.Migrations.Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: ContentId, DocumentId columns and indexes were added via manual SQL script.
        // This migration exists to keep EF model snapshot in sync with the database.
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
