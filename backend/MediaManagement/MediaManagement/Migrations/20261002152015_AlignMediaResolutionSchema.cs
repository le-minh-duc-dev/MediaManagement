using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaManagement.Migrations
{
    /// <inheritdoc />
    public partial class AlignMediaResolutionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImageResolutions_MediaAssets_MediaAssetId",
                table: "ImageResolutions");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoResolutions_MediaAssets_MediaAssetId",
                table: "VideoResolutions");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "ImageResolutions");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "ImageResolutions");

            migrationBuilder.AddForeignKey(
                name: "FK_ImageResolutions_MediaAssets_MediaAssetId",
                table: "ImageResolutions",
                column: "MediaAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoResolutions_MediaAssets_MediaAssetId",
                table: "VideoResolutions",
                column: "MediaAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImageResolutions_MediaAssets_MediaAssetId",
                table: "ImageResolutions");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoResolutions_MediaAssets_MediaAssetId",
                table: "VideoResolutions");

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "ImageResolutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "ImageResolutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_ImageResolutions_MediaAssets_MediaAssetId",
                table: "ImageResolutions",
                column: "MediaAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoResolutions_MediaAssets_MediaAssetId",
                table: "VideoResolutions",
                column: "MediaAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
