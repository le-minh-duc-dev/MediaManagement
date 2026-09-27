using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddPostPagination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert existing EF SQLite timestamp strings before the table rebuild.
            // Parse whole seconds separately to preserve all seven fractional digits.
            migrationBuilder.Sql("""
                UPDATE "Posts" SET "CreatedAt" =
                    (unixepoch(substr("CreatedAt", 1, 19) || substr("CreatedAt", -6)) + 62135596800) * 10000000
                    + CASE WHEN substr("CreatedAt", 20, 1) = '.'
                        THEN CAST(substr(substr("CreatedAt", 21, length("CreatedAt") - 26) || '0000000', 1, 7) AS INTEGER)
                        ELSE 0 END
                WHERE instr("CreatedAt", ':') > 0;
                """);
            migrationBuilder.AlterColumn<long>(
                name: "CreatedAt",
                table: "Posts",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_OwnerId_CreatedAt_Id",
                table: "Posts",
                columns: new[] { "OwnerId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_OwnerId_CreatedAt_Id",
                table: "Posts");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Posts",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.Sql("""
                UPDATE "Posts" SET "CreatedAt" =
                    strftime('%Y-%m-%d %H:%M:%S', CAST("CreatedAt" AS INTEGER) / 10000000 - 62135596800, 'unixepoch')
                    || printf('.%07d+00:00', CAST("CreatedAt" AS INTEGER) % 10000000)
                WHERE instr("CreatedAt", ':') = 0;
                """);
        }
    }
}
