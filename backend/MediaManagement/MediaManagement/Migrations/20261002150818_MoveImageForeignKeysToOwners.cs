using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaManagement.Migrations
{
    /// <inheritdoc />
    public partial class MoveImageForeignKeysToOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A one-to-one thumbnail cannot represent multiple existing thumbnails.
            // Fail before changing the schema rather than silently choosing an image.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__ThumbnailMigrationCheck" (
                    "ThumbnailCount" INTEGER NOT NULL,
                    CONSTRAINT "A_video_must_have_at_most_one_thumbnail"
                        CHECK ("ThumbnailCount" <= 1)
                );
                INSERT INTO "__ThumbnailMigrationCheck" ("ThumbnailCount")
                SELECT COUNT(*) FROM "Images"
                WHERE "VideoId" IS NOT NULL GROUP BY "VideoId";
                DROP TABLE "__ThumbnailMigrationCheck";
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "ThumbnailImageId",
                table: "Videos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Videos"
                SET "ThumbnailImageId" = (
                    SELECT "Id" FROM "Images" WHERE "VideoId" = "Videos"."Id"
                );
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Videos_VideoId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_VideoId",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "VideoId",
                table: "Images");

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    AvatarImageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Images_AvatarImageId",
                        column: x => x.AvatarImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_ThumbnailImageId",
                table: "Videos",
                column: "ThumbnailImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AvatarImageId",
                table: "UserProfiles",
                column: "AvatarImageId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Images_ThumbnailImageId",
                table: "Videos",
                column: "ThumbnailImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VideoId",
                table: "Images",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Images"
                SET "VideoId" = (
                    SELECT "Id" FROM "Videos" WHERE "ThumbnailImageId" = "Images"."Id"
                );
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Images_ThumbnailImageId",
                table: "Videos");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Videos_ThumbnailImageId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ThumbnailImageId",
                table: "Videos");

            migrationBuilder.CreateIndex(
                name: "IX_Images_VideoId",
                table: "Images",
                column: "VideoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Videos_VideoId",
                table: "Images",
                column: "VideoId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
