using MediaManagement.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MediaManagement.Database;

public sealed class MediaManagementContext(DbContextOptions<MediaManagementContext> options)
    : IdentityDbContext(options)
{
    public DbSet<Post> Posts { get; set; }
    public DbSet<UploadSession> UploadSessions { get; set; }
    public DbSet<MediaAsset> MediaAssets { get; set; }
    public DbSet<Image> Images { get; set; }
    public DbSet<ImageResolution> ImageResolutions { get; set; }
    public DbSet<Video> Videos { get; set; }
    public DbSet<VideoResolution> VideoResolutions { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder
            .Entity<Post>()
            .Property(x => x.CreatedAt)
            .HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        modelBuilder
            .Entity<Post>()
            .HasIndex(x => new
            {
                x.CreatedBy,
                x.CreatedAt,
                x.Id,
            });
        var sessions = modelBuilder.Entity<UploadSession>();
        sessions.Property(x => x.Revision).IsConcurrencyToken();
        // UTC ticks allow SQLite to compare and order DateTimeOffset values server-side.
        sessions
            .Property(x => x.ExpiresAt)
            .HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        sessions
            .Property(x => x.NextCleanupAt)
            .HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        sessions.HasIndex(x => new { x.CleanedUpAt, x.NextCleanupAt });
        sessions
            .HasMany(x => x.MediaAssets)
            .WithOne(x => x.UploadSession)
            .HasForeignKey(x => x.UploadSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MediaAsset>().HasIndex(x => x.ObjectKey).IsUnique();
        modelBuilder.Entity<MediaAsset>().Property(x => x.FileName).HasMaxLength(255);
        modelBuilder.Entity<MediaAsset>().Property(x => x.ContentType);
        modelBuilder
            .Entity<PostItem>()
            .HasOne(x => x.MediaAsset)
            .WithMany()
            .HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder
            .Entity<Video>()
            .HasOne(x => x.Thumbnail)
            .WithOne()
            .HasForeignKey<Video>(x => x.ThumbnailImageId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder
            .Entity<Image>()
            .HasMany(x => x.Resolutions)
            .WithOne(x => x.Image)
            .HasForeignKey(x => x.ImageId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<Video>()
            .HasMany(x => x.Resolutions)
            .WithOne(x => x.Video)
            .HasForeignKey(x => x.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<ImageResolution>()
            .HasOne(x => x.MediaAsset)
            .WithMany()
            .HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<VideoResolution>()
            .HasOne(x => x.MediaAsset)
            .WithMany()
            .HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<UserProfile>()
            .HasOne(user => user.Avatar)
            .WithOne()
            .HasForeignKey<UserProfile>(user => user.AvatarImageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
