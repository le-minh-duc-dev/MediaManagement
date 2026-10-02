using MediaManagement.Database;
using MediaManagement.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MediaManagement.IntegrationTests;

public sealed class MediaContentTypePersistenceTests
{
    [Theory]
    [InlineData(MediaContentType.Jpeg, "image/jpeg")]
    [InlineData(MediaContentType.Png, "image/png")]
    [InlineData(MediaContentType.Webp, "image/webp")]
    [InlineData(MediaContentType.Gif, "image/gif")]
    [InlineData(MediaContentType.Mp4, "video/mp4")]
    public async Task Content_type_is_stored_as_mime_string_and_queried_as_enum(
        MediaContentType contentType,
        string mimeType
    )
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MediaManagementContext(
            new DbContextOptionsBuilder<MediaManagementContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());

        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            FileName = "media",
            ObjectKey = Guid.NewGuid().ToString(),
            ContentType = contentType,
        };
        var session = new UploadSession { Id = Guid.NewGuid(), MediaAssets = [asset] };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ContentType FROM MediaAssets";
        Assert.Equal(mimeType, await command.ExecuteScalarAsync());

        db.ChangeTracker.Clear();
        var loaded = await db.MediaAssets.SingleAsync(x => x.ContentType == contentType);
        Assert.Equal(asset.Id, loaded.Id);
        Assert.Equal(contentType, loaded.ContentType);
    }
}
