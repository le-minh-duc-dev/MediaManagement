using MediaManagement.Database;
using MediaManagement.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MediaManagement.IntegrationTests;

public sealed class MediaResolutionTests
{
    [Fact]
    public async Task Migration_persists_resolutions_and_thumbnail_and_preserves_stored_assets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MediaManagementContext(
            new DbContextOptionsBuilder<MediaManagementContext>().UseSqlite(connection).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261001150151_initApp");

        var session = new UploadSession { Id = Guid.NewGuid() };
        MediaAsset CreateAsset(MediaContentType contentType)
        {
            var asset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                ObjectKey = Guid.NewGuid().ToString(),
                FileName = "variant",
                ContentType = contentType,
            };
            session.MediaAssets.Add(asset);
            return asset;
        }

        var original = CreateAsset(MediaContentType.Jpeg);
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        Assert.Equal(original.ObjectKey, (await db.MediaAssets.AsNoTracking().SingleAsync()).ObjectKey);

        Image CreateImage() => new()
        {
            Id = Guid.NewGuid(),
            Resolutions =
            [
                new() { Id = Guid.NewGuid(), MediaAsset = CreateAsset(MediaContentType.Jpeg) },
                new() { Id = Guid.NewGuid(), MediaAsset = CreateAsset(MediaContentType.Jpeg) },
            ],
        };

        var image = CreateImage();
        var video = new Video
        {
            Id = Guid.NewGuid(),
            Resolutions =
            [
                new() { Id = Guid.NewGuid(), Width = 1920, Height = 1080, MediaAsset = CreateAsset(MediaContentType.Mp4) },
                new() { Id = Guid.NewGuid(), Width = 1280, Height = 720, MediaAsset = CreateAsset(MediaContentType.Mp4) },
            ],
            Thumbnail = CreateImage(),
        };
        db.Images.Add(image);
        db.Videos.Add(video);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var savedImage = await db.Images.Include(x => x.Resolutions).ThenInclude(x => x.MediaAsset)
            .SingleAsync(x => x.Id == image.Id);
        Assert.Equal(image.Resolutions.Select(x => x.Id).Order(), savedImage.Resolutions.Select(x => x.Id).Order());
        Assert.All(savedImage.Resolutions, x => Assert.Equal(MediaContentType.Jpeg, x.MediaAsset!.ContentType));

        var savedVideo = await db.Videos.Include(x => x.Resolutions).ThenInclude(x => x.MediaAsset)
            .Include(x => x.Thumbnail).ThenInclude(x => x!.Resolutions).ThenInclude(x => x.MediaAsset)
            .SingleAsync(x => x.Id == video.Id);
        Assert.Equal([720, 1080], savedVideo.Resolutions.Select(x => x.Height).Order().ToArray());
        Assert.All(savedVideo.Resolutions, x => Assert.Equal(MediaContentType.Mp4, x.MediaAsset!.ContentType));
        var thumbnail = Assert.IsType<Image>(savedVideo.Thumbnail);
        Assert.Equal(video.Thumbnail!.Id, thumbnail.Id);
        Assert.Equal(thumbnail.Id, savedVideo.ThumbnailImageId);
        Assert.Equal(2, thumbnail.Resolutions.Count);
        Assert.All(thumbnail.Resolutions, x => Assert.Equal(MediaContentType.Jpeg, x.MediaAsset!.ContentType));

        var videoResolution = savedVideo.Resolutions.First();
        db.ChangeTracker.Clear();
        await db.MediaAssets.Where(x => x.Id == videoResolution.MediaAssetId).ExecuteDeleteAsync();
        Assert.False(await db.VideoResolutions.AnyAsync(x => x.Id == videoResolution.Id));
        Assert.Equal(1, await db.VideoResolutions.CountAsync());
        Assert.True(await db.Videos.AnyAsync(x => x.Id == video.Id));

        // Exercise database cascades without relying on tracked navigation properties.
        db.ChangeTracker.Clear();
        await db.Videos.Where(x => x.Id == video.Id).ExecuteDeleteAsync();
        Assert.Empty(await db.VideoResolutions.ToListAsync());
        Assert.Equal(2, await db.Images.CountAsync());
        Assert.Equal(4, await db.ImageResolutions.CountAsync());
        Assert.Equal(6, await db.MediaAssets.CountAsync());

        await db.Images.Where(x => x.Id == thumbnail.Id).ExecuteDeleteAsync();
        Assert.Equal(image.Id, (await db.Images.SingleAsync()).Id);
        Assert.Equal(2, await db.ImageResolutions.CountAsync());
        Assert.Equal(6, await db.MediaAssets.CountAsync());

        // Deleting a stored asset cascades to its resolution metadata.
        var resolution = savedImage.Resolutions.First();
        await db.MediaAssets.Where(x => x.Id == resolution.MediaAssetId).ExecuteDeleteAsync();
        Assert.False(await db.ImageResolutions.AnyAsync(x => x.Id == resolution.Id));
        Assert.Equal(1, await db.ImageResolutions.CountAsync());
        Assert.Equal(5, await db.MediaAssets.CountAsync());
    }
}
