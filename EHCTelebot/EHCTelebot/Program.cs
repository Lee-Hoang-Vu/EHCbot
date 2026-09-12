using EHCTelebot.Data;
using EHCTelebot.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// DATABASE
// ========================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ========================================
// TELEGRAM
// ========================================

builder.Services.AddSingleton<TelegramService>();

builder.Services.AddScoped<TelegramUpdateHandler>();

builder.Services.AddScoped<DailyNotificationService>();

// KHÔNG chạy polling trên production.
// Local development có thể dùng riêng nếu cần.
// builder.Services.AddHostedService<TelegramPollingService>();

// ========================================
// RENDER PORT
// ========================================

var port =
    Environment.GetEnvironmentVariable("PORT") ?? "10000";

builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}");

// ========================================
// APPLICATION
// ========================================

var app = builder.Build();

// ========================================
// BASIC
// ========================================

app.MapGet("/", () =>
    "EHC Telegram Bot is running!");

// ========================================
// HEALTH CHECK
// ========================================

app.MapGet("/api/ping", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        time = DateTime.UtcNow
    });
});

// ========================================
// DATABASE TEST
// ========================================

app.MapGet("/api/test-db", async (AppDbContext db) =>
{
    try
    {
        var count = await db.Users.CountAsync();

        return Results.Ok(new
        {
            status = "success",
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

// ========================================
// TELEGRAM WEBHOOK
// ========================================

app.MapPost(
    "/api/telegram-webhook",
    async (
        HttpRequest request,
        TelegramUpdateHandler handler,
        IConfiguration configuration,
        Update update) =>
    {
        var expectedSecret =
            configuration["Telegram:WebhookSecret"];

        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            return Results.StatusCode(500);
        }

        if (!request.Headers.TryGetValue(
                "X-Telegram-Bot-Api-Secret-Token",
                out var receivedSecret))
        {
            return Results.Unauthorized();
        }

        if (receivedSecret != expectedSecret)
        {
            return Results.Unauthorized();
        }

        await handler.HandleAsync(update);

        return Results.Ok();
    });

// ========================================
// DAILY NOTIFICATION
// ========================================

app.MapPost(
    "/api/trigger-daily",
    async (
        HttpRequest request,
        DailyNotificationService service,
        IConfiguration configuration,
        CancellationToken cancellationToken) =>
    {
        var expectedKey =
            configuration["Cron:ApiKey"];

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return Results.StatusCode(500);
        }

        if (!request.Headers.TryGetValue(
                "X-Cron-Key",
                out var receivedKey))
        {
            return Results.Unauthorized();
        }

        if (receivedKey != expectedKey)
        {
            return Results.Unauthorized();
        }

        var result =
            await service.SendDailyNotificationsAsync(
                cancellationToken);

        return Results.Ok(result);
    });

app.Run();