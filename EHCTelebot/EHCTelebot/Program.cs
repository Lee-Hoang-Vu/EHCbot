using EHCTelebot.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.MapGet("/", () => "EHC Telegram Bot is running!");

app.MapGet("/api/ping", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        time = DateTime.UtcNow
    });
});
app.MapGet("/api/test-db", async (AppDbContext db) =>
{
    try
    {
        var count = await db.Users.CountAsync();

        return Results.Ok(new
        {
            status = "success",
            message = "Connected to Neon PostgreSQL successfully.",
            userCount = count
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "Database connection failed");
    }
});
app.Run();