using EHCTelebot.Data;
using Microsoft.EntityFrameworkCore;
<<<<<<< HEAD
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
=======
>>>>>>> parent of e40b04b (Demo)

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

<<<<<<< HEAD
// ========================================
// TELEGRAM
// ========================================

builder.Services.AddSingleton<TelegramService>();

builder.Services.AddScoped<TelegramUpdateHandler>();

builder.Services.AddScoped<DailyNotificationService>();

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
// TELEGRAM WEBHOOK SETUP
// ========================================

var telegramBotToken =
    builder.Configuration["Telegram:BotToken"];

var webhookSecret =
    builder.Configuration["Telegram:WebhookSecret"];

var renderUrl =
    Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL");

if (string.IsNullOrWhiteSpace(renderUrl))
{
    renderUrl = "https://ehcbot.onrender.com";
}

if (string.IsNullOrWhiteSpace(telegramBotToken))
{
    throw new InvalidOperationException(
        "Telegram:BotToken is not configured.");
}

if (string.IsNullOrWhiteSpace(webhookSecret))
{
    throw new InvalidOperationException(
        "Telegram:WebhookSecret is not configured.");
}

var webhookUrl =
    $"{renderUrl.TrimEnd('/')}/api/telegram-webhook";

try
{
    var botClient =
        new TelegramBotClient(telegramBotToken);

    await botClient.SetWebhookAsync(
        webhookUrl,
        secretToken: webhookSecret);

    Console.WriteLine("========================================");
    Console.WriteLine("TELEGRAM WEBHOOK");
    Console.WriteLine("========================================");
    Console.WriteLine($"Webhook URL: {webhookUrl}");
    Console.WriteLine("Webhook registered successfully.");
    Console.WriteLine("========================================");
}
catch (Exception ex)
{
    Console.WriteLine("========================================");
    Console.WriteLine("TELEGRAM WEBHOOK ERROR");
    Console.WriteLine("========================================");
    Console.WriteLine(ex);
    Console.WriteLine("========================================");
}

// ========================================
// BASIC
// ========================================

app.MapGet("/", () =>
    "EHC Telegram Bot is running!");

// ========================================
// HEALTH CHECK
// ========================================
=======
var app = builder.Build();

app.MapGet("/", () => "EHC Telegram Bot is running!");
>>>>>>> parent of e40b04b (Demo)

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
<<<<<<< HEAD

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

        try
        {
            Console.WriteLine(
                $"Telegram update received: {update.Id}");

            await handler.HandleAsync(update);

            return Results.Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Telegram update error: {ex}");

            return Results.StatusCode(500);
        }
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

// ========================================
// START
// ========================================

=======
>>>>>>> parent of e40b04b (Demo)
app.Run();