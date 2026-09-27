using MediaManagement;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddServices(builder.Configuration, builder.Environment);

WebApplication app = builder.Build();

app.RegisterMiddlewares();

app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
