using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Credo.BlobStorage.Migrator.Data.Migration.Migrations;

/// <inheritdoc />
public partial class AddWorkerToken : Microsoft.EntityFrameworkCore.Migrations.Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: WorkerToken column and index were added via manual SQL script.
        // This migration exists to keep EF model snapshot in sync with the database.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MigrationLog_WorkerToken",
            schema: "migration",
            table: "MigrationLog");

        migrationBuilder.DropColumn(
            name: "WorkerToken",
            schema: "migration",
            table: "MigrationLog");
    }
}
