using MediaManagement.Database;
using MediaManagement.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MediaManagement.IntegrationTests;

public sealed class PostMigrationTests
{
    [Fact]
    public async Task Migration_preserves_existing_timestamp_precision_and_relationships()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MediaManagementContext(new DbContextOptionsBuilder<MediaManagementContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        const string initial = "20260927085036_InitialMediaSchema";
        await migrator.MigrateAsync(initial);
        DateTimeOffset[] timestamps = [
            new DateTimeOffset(2026, 9, 27, 11, 22, 33, TimeSpan.FromHours(7)).AddTicks(9999999),
            new DateTimeOffset(2026, 9, 27, 11, 22, 33, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 27, 11, 22, 33, TimeSpan.FromHours(-4)).AddTicks(1234000)
        ];
        List<Guid> ids = [];
        foreach (var timestamp in timestamps)
        {
            Guid id = Guid.NewGuid();
            ids.Add(id);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Posts (Id, OwnerId, CreatedAt) VALUES ({id}, {Guid.NewGuid()}, {timestamp})");
        }
        Guid tagId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Tag (Id, Name) VALUES ({tagId}, {"existing"})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PostTag (PostsId, TagsId) VALUES ({ids[0]}, {tagId})");
        await migrator.MigrateAsync();
        for (int index = 0; index < ids.Count; index++)
        {
            Post post = await db.Posts.AsNoTracking().Include(x => x.Tags).SingleAsync(x => x.Id == ids[index]);
            Assert.Equal(timestamps[index].UtcTicks, post.CreatedAt.UtcTicks);
            if (index == 0)
                Assert.Equal(tagId, Assert.Single(post.Tags).Id);
        }
        await migrator.MigrateAsync(initial);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CreatedAt FROM Posts WHERE Id = $id";
        command.Parameters.AddWithValue("$id", ids[0]);
        Assert.Equal(timestamps[0].UtcTicks, DateTimeOffset.Parse((string)(await command.ExecuteScalarAsync())!).UtcTicks);
    }
}