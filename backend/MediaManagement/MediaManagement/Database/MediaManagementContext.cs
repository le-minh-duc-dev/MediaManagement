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
                x.OwnerId,
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
        modelBuilder.Entity<MediaAsset>().Property(x => x.ContentType).HasMaxLength(127);
        modelBuilder
            .Entity<PostItem>()
            .HasOne(x => x.MediaAsset)
            .WithMany()
            .HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
