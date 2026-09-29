using MediaManagement.Boostrap;
using MediaManagement.Database;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServices(builder.Configuration, builder.Environment);

var app = builder.Build();

await Migrator.RunMigrationAsync(app);

app.RegisterMiddlewares();

app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
