using MediaManagement.Database;
using MediaManagement.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MediaManagement.IntegrationTests;

public sealed class ImageRelationshipTests
{
    [Fact]
    public async Task Owners_persist_image_foreign_keys_and_deleting_owners_preserves_image()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var image = new Image { Id = Guid.NewGuid() };
        var profile = new UserProfile { Id = Guid.NewGuid(), UserId = "user", Avatar = image };
        var video = new Video { Id = Guid.NewGuid(), Thumbnail = image };
        db.AddRange(profile, video);
        db.AddRange(
            new UserProfile { Id = Guid.NewGuid(), UserId = "without-avatar" },
            new Video { Id = Guid.NewGuid() });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Equal(image.Id, (await db.UserProfiles.Include(x => x.Avatar)
            .SingleAsync(x => x.Id == profile.Id)).AvatarImageId);
        Assert.Equal(image.Id, (await db.Videos.Include(x => x.Thumbnail)
            .SingleAsync(x => x.Id == video.Id)).ThumbnailImageId);

        // Sharing across different relationships is allowed; referenced images are protected.
        await Assert.ThrowsAsync<SqliteException>(() => db.Images.ExecuteDeleteAsync());
        db.ChangeTracker.Clear();
        await db.UserProfiles.ExecuteDeleteAsync();
        await db.Videos.ExecuteDeleteAsync();
        Assert.Equal(image.Id, (await db.Images.SingleAsync()).Id);
        await db.Images.ExecuteDeleteAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_image_cannot_be_reused_within_the_same_relationship(bool avatar)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        var image = new Image { Id = Guid.NewGuid() };
        db.Images.Add(image);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        if (avatar)
            db.UserProfiles.Add(new() { Id = Guid.NewGuid(), UserId = "first", AvatarImageId = image.Id });
        else
            db.Videos.Add(new() { Id = Guid.NewGuid(), ThumbnailImageId = image.Id });

        await db.SaveChangesAsync();
        // A separate unit of work prevents relationship fixup from replacing the first owner.
        db.ChangeTracker.Clear();
        if (avatar)
            db.UserProfiles.Add(new() { Id = Guid.NewGuid(), UserId = "second", AvatarImageId = image.Id });
        else
            db.Videos.Add(new() { Id = Guid.NewGuid(), ThumbnailImageId = image.Id });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(19, Assert.IsType<SqliteException>(exception.InnerException).SqliteErrorCode);
    }

    [Fact]
    public async Task Migration_moves_existing_thumbnail_link_and_restores_it_on_rollback()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateMigrationContext(connection);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261001155459_AddImagesAndVideos");
        var videoId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        await InsertLegacyThumbnail(db, videoId, imageId);

        await migrator.MigrateAsync("20261002150818_MoveImageForeignKeysToOwners");
        Assert.Equal(imageId, (await db.Videos.AsNoTracking().SingleAsync()).ThumbnailImageId);
        Assert.Equal(imageId, (await db.Images.AsNoTracking().SingleAsync()).Id);
        await Assert.ThrowsAsync<SqliteException>(() => db.Images.ExecuteDeleteAsync());

        await migrator.MigrateAsync("20261001155459_AddImagesAndVideos");
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT VideoId FROM Images";
        Assert.Equal(videoId.ToString().ToUpperInvariant(), await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Migration_rejects_multiple_legacy_thumbnails_without_losing_links()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateMigrationContext(connection);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261001155459_AddImagesAndVideos");
        var videoId = Guid.NewGuid();
        await InsertLegacyThumbnail(db, videoId, Guid.NewGuid());
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Images (Id, VideoId, CreatedAt, CreatedBy)
            VALUES ({Guid.NewGuid()}, {videoId}, '2026-10-02', {Guid.Empty})
            """);

        await Assert.ThrowsAsync<SqliteException>(() =>
            migrator.MigrateAsync("20261002150818_MoveImageForeignKeysToOwners"));
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Images WHERE VideoId IS NOT NULL";
        Assert.Equal(2L, await command.ExecuteScalarAsync());
    }

    private static MediaManagementContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<MediaManagementContext>().UseSqlite(connection).Options);

    private static MediaManagementContext CreateMigrationContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<MediaManagementContext>()
            .UseSqlite(connection)
            .Options);

    private static async Task InsertLegacyThumbnail(MediaManagementContext db, Guid videoId, Guid imageId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Videos (Id, CreatedAt, CreatedBy)
            VALUES ({videoId}, '2026-10-02', {Guid.Empty})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Images (Id, VideoId, CreatedAt, CreatedBy)
            VALUES ({imageId}, {videoId}, '2026-10-02', {Guid.Empty})
            """);
    }
}
