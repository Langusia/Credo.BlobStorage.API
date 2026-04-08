using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credo.BlobStorage.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Objects_Bucket_Filename",
                schema: "blobStorage",
                table: "Objects");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UQ_Objects_Bucket_Filename",
                schema: "blobStorage",
                table: "Objects",
                columns: new[] { "Bucket", "Filename" },
                unique: true);
        }
    }
}
