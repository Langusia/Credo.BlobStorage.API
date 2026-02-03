using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Credo.BlobStorage.Api.Data.Migrations;

/// <inheritdoc />
public partial class ApiInitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Ensure schema exists
        migrationBuilder.EnsureSchema(name: "blobStorage");

        migrationBuilder.CreateTable(
            name: "Buckets",
            schema: "blobStorage",
            columns: table => new
            {
                Name = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Buckets", x => x.Name);
            });

        migrationBuilder.CreateTable(
            name: "Objects",
            schema: "blobStorage",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Bucket = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                Filename = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                DocId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                ServedContentType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                DetectedContentType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                ClaimedContentType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                DetectedExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                DetectionMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                IsMismatch = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                IsDangerousMismatch = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Objects", x => x.Id);
                table.ForeignKey(
                    name: "FK_Objects_Buckets_Bucket",
                    column: x => x.Bucket,
                    principalSchema: "blobStorage",
                    principalTable: "Buckets",
                    principalColumn: "Name",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Objects_Bucket",
            schema: "blobStorage",
            table: "Objects",
            column: "Bucket");

        migrationBuilder.CreateIndex(
            name: "IX_Objects_DocId",
            schema: "blobStorage",
            table: "Objects",
            column: "DocId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UQ_Objects_Bucket_Filename",
            schema: "blobStorage",
            table: "Objects",
            columns: new[] { "Bucket", "Filename" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Objects",
            schema: "blobStorage");

        migrationBuilder.DropTable(
            name: "Buckets",
            schema: "blobStorage");
    }
}
