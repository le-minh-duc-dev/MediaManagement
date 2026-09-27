using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaManagement.Migrations;

/// <inheritdoc />
public partial class InitialMediaSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Posts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                Caption = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Posts", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "Tag",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tag", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "UploadSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                ExpectedSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                NextCleanupAt = table.Column<long>(type: "INTEGER", nullable: false),
                CleanedUpAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Revision = table.Column<Guid>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UploadSessions", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "PostTag",
            columns: table => new
            {
                PostsId = table.Column<Guid>(type: "TEXT", nullable: false),
                TagsId = table.Column<Guid>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PostTag", x => new { x.PostsId, x.TagsId });
                table.ForeignKey(
                    name: "FK_PostTag_Posts_PostsId",
                    column: x => x.PostsId,
                    principalTable: "Posts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_PostTag_Tag_TagsId",
                    column: x => x.TagsId,
                    principalTable: "Tag",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "MediaAssets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                UploadSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                ObjectKey = table.Column<string>(type: "TEXT", nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "TEXT", maxLength: 127, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UploadedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaAssets", x => x.Id);
                table.ForeignKey(
                    name: "FK_MediaAssets_UploadSessions_UploadSessionId",
                    column: x => x.UploadSessionId,
                    principalTable: "UploadSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "PostItem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                MediaAssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                AltText = table.Column<string>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PostItem", x => x.Id);
                table.ForeignKey(
                    name: "FK_PostItem_MediaAssets_MediaAssetId",
                    column: x => x.MediaAssetId,
                    principalTable: "MediaAssets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    name: "FK_PostItem_Posts_PostId",
                    column: x => x.PostId,
                    principalTable: "Posts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_MediaAssets_ObjectKey",
            table: "MediaAssets",
            column: "ObjectKey",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_MediaAssets_UploadSessionId",
            table: "MediaAssets",
            column: "UploadSessionId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_PostItem_MediaAssetId",
            table: "PostItem",
            column: "MediaAssetId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_PostItem_PostId",
            table: "PostItem",
            column: "PostId"
        );

        migrationBuilder.CreateIndex(name: "IX_PostTag_TagsId", table: "PostTag", column: "TagsId");

        migrationBuilder.CreateIndex(
            name: "IX_UploadSessions_CleanedUpAt_NextCleanupAt",
            table: "UploadSessions",
            columns: new[] { "CleanedUpAt", "NextCleanupAt" }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PostItem");

        migrationBuilder.DropTable(name: "PostTag");

        migrationBuilder.DropTable(name: "MediaAssets");

        migrationBuilder.DropTable(name: "Posts");

        migrationBuilder.DropTable(name: "Tag");

        migrationBuilder.DropTable(name: "UploadSessions");
    }
}
