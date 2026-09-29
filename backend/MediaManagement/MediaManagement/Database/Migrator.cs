using Microsoft.EntityFrameworkCore;

namespace MediaManagement.Database;

public static class Migrator
{
    public static async Task RunMigrationAsync(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaManagementContext>();
            await db.Database.MigrateAsync();
        }
    }
}
