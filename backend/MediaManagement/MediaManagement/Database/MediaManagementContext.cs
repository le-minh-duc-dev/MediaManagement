using MediaManagement.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaManagement.Database;

public sealed class MediaManagementContext(DbContextOptions<MediaManagementContext> options)
    : DbContext(options)
{
    public DbSet<Post> Posts { get; set; }
    public DbSet<UploadSession> UploadSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
