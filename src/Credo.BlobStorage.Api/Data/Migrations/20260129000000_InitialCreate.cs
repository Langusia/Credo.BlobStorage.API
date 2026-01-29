using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Credo.BlobStorage.Api.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Buckets",
            columns: table => new
            {
                Name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Buckets", x => x.Name);
            });

        migrationBuilder.CreateTable(
            name: "Objects",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                Bucket = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                Filename = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                DocId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Year = table.Column<int>(type: "integer", nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                ServedContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                DetectedContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                ClaimedContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                DetectedExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                DetectionMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                IsMismatch = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsDangerousMismatch = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Objects", x => x.Id);
                table.ForeignKey(
                    name: "FK_Objects_Buckets_Bucket",
                    column: x => x.Bucket,
                    principalTable: "Buckets",
                    principalColumn: "Name",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Objects_Bucket",
            table: "Objects",
            column: "Bucket");

        migrationBuilder.CreateIndex(
            name: "IX_Objects_DocId",
            table: "Objects",
            column: "DocId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UQ_Objects_Bucket_Filename",
            table: "Objects",
            columns: new[] { "Bucket", "Filename" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Objects");

        migrationBuilder.DropTable(
            name: "Buckets");
    }
}
