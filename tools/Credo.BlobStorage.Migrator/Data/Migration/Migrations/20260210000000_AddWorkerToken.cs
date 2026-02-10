using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Credo.BlobStorage.Migrator.Data.Migration.Migrations;

/// <inheritdoc />
public partial class AddWorkerToken : Microsoft.EntityFrameworkCore.Migrations.Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WorkerToken",
            schema: "migration",
            table: "MigrationLog",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_MigrationLog_WorkerToken",
            schema: "migration",
            table: "MigrationLog",
            column: "WorkerToken");
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
