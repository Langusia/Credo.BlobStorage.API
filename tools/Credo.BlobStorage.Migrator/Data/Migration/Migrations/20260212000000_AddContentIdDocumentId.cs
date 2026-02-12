using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Credo.BlobStorage.Migrator.Data.Migration.Migrations;

/// <inheritdoc />
public partial class AddContentIdDocumentId : Microsoft.EntityFrameworkCore.Migrations.Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: columns/indexes may already exist if added via manual script
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('[migration].[MigrationLog]') AND name = 'ContentId'
            )
            ALTER TABLE [migration].[MigrationLog] ADD [ContentId] BIGINT NULL;
            """);

        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('[migration].[MigrationLog]') AND name = 'DocumentId'
            )
            ALTER TABLE [migration].[MigrationLog] ADD [DocumentId] BIGINT NULL;
            """);

        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID('[migration].[MigrationLog]') AND name = 'IX_MigrationLog_SourceYear_ContentId'
            )
            CREATE INDEX [IX_MigrationLog_SourceYear_ContentId]
                ON [migration].[MigrationLog] ([SourceYear], [ContentId]);
            """);

        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID('[migration].[MigrationLog]') AND name = 'IX_MigrationLog_SourceYear_DocumentId'
            )
            CREATE INDEX [IX_MigrationLog_SourceYear_DocumentId]
                ON [migration].[MigrationLog] ([SourceYear], [DocumentId]);
            """);
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
