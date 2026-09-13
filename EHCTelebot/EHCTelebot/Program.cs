using EHCTelebot.Data;
using EHCTelebot.Services;
using Microsoft.EntityFrameworkCore;
using TelegramUpdate = Telegram.Bot.Types.Update;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// DATABASE
// =====================================================

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// =====================================================
// SERVICES
// =====================================================

builder.Services.AddSingleton<TelegramService>();
builder.Services.AddScoped<TelegramUpdateHandler>();
builder.Services.AddScoped<DailyNotificationService>();

// =====================================================
// PORT
// =====================================================

var port =
    Environment.GetEnvironmentVariable("PORT")
    ?? "10000";

builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}");

// =====================================================
// APPLICATION
// =====================================================

var app = builder.Build();

// =====================================================
// ROOT
// =====================================================

app.MapGet("/", () =>
    "EHC Telegram Bot is running!");

// =====================================================
// PING
// =====================================================

app.MapGet("/api/ping", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        time = DateTime.UtcNow
    });
});

// =====================================================
// DATABASE TEST
// =====================================================

app.MapGet("/api/test-db", async (AppDbContext db) =>
{
    try
    {
        var count =
            await db.Users.CountAsync();

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

// =====================================================
// TELEGRAM WEBHOOK
// =====================================================

app.MapPost(
    "/api/telegram-webhook",
    async (HttpRequest request) =>
    {
        try
        {
            Console.WriteLine("=== TELEGRAM WEBHOOK HIT ===");

            Console.WriteLine(
                $"Method: {request.Method}");

            Console.WriteLine(
                $"Content-Type: {request.ContentType}");

            if (request.Headers.TryGetValue(
                    "X-Telegram-Bot-Api-Secret-Token",
                    out var secret))
            {
                Console.WriteLine("Secret header received.");
            }
            else
            {
                Console.WriteLine("Secret header MISSING.");
            }

            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();

            Console.WriteLine(
                $"Body length: {body.Length}");

            Console.WriteLine(
                $"Body: {body}");

            return Results.Ok(new
            {
                status = "received"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("WEBHOOK TEST ERROR:");
            Console.WriteLine(ex.ToString());

            return Results.StatusCode(500);
        }
    });
// =====================================================
// DAILY NOTIFICATION
// =====================================================

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