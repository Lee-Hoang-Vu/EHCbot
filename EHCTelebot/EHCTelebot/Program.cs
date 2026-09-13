using EHCTelebot.Data;
using EHCTelebot.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

builder.Services.AddSingleton<TelegramService>();
builder.Services.AddScoped<TelegramUpdateHandler>();
builder.Services.AddScoped<DailyNotificationService>();

var port =
    Environment.GetEnvironmentVariable("PORT")
    ?? "10000";

builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}");

var app = builder.Build();


// ============================================================
// HEALTH CHECK
// ============================================================

app.MapGet("/", () =>
    "EHC Telegram Bot is running!");


app.MapGet("/api/ping", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        time = DateTime.UtcNow
    });
});


// ============================================================
// DATABASE TEST
// ============================================================

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


// ============================================================
// TELEGRAM WEBHOOK
// ============================================================

app.MapPost(
    "/api/telegram-webhook",
    async (
        HttpRequest request,
        TelegramUpdateHandler handler,
        IConfiguration configuration) =>
    {
        try
        {
            // ------------------------------------------------
            // 1. Check Telegram secret
            // ------------------------------------------------

            var expectedSecret =
                configuration["Telegram:WebhookSecret"];

            if (string.IsNullOrWhiteSpace(expectedSecret))
            {
                Console.WriteLine(
                    "Telegram:WebhookSecret is missing.");

                return Results.StatusCode(500);
            }

            if (!request.Headers.TryGetValue(
                    "X-Telegram-Bot-Api-Secret-Token",
                    out var receivedSecret))
            {
                Console.WriteLine(
                    "Telegram secret header is missing.");

                return Results.Unauthorized();
            }

            if (receivedSecret != expectedSecret)
            {
                Console.WriteLine(
                    "Telegram secret is invalid.");

                return Results.Unauthorized();
            }


            // ------------------------------------------------
            // 2. Read Telegram JSON
            // ------------------------------------------------

            using var reader =
                new StreamReader(request.Body);

            var body =
                await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                Console.WriteLine(
                    "Telegram webhook body is empty.");

                return Results.BadRequest();
            }


            // ------------------------------------------------
            // 3. Deserialize to our own DTO
            // ------------------------------------------------

            var update =
                JsonSerializer.Deserialize<TelegramWebhookUpdate>(
                    body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (update == null)
            {
                Console.WriteLine(
                    "Cannot deserialize Telegram update.");

                return Results.BadRequest();
            }


            // ------------------------------------------------
            // 4. Ignore updates without message
            // ------------------------------------------------

            if (update.Message == null)
            {
                return Results.Ok();
            }

            if (update.Message.Chat == null)
            {
                return Results.Ok();
            }


            // ------------------------------------------------
            // 5. Process bot command/message
            // ------------------------------------------------

            await handler.HandleAsync(update);


            // ------------------------------------------------
            // 6. Telegram requires successful HTTP response
            // ------------------------------------------------

            return Results.Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "========== TELEGRAM WEBHOOK ERROR ==========");

            Console.WriteLine(ex);

            Console.WriteLine(
                "============================================");

            return Results.StatusCode(500);
        }
    });


// ============================================================
// DAILY NOTIFICATION
// ============================================================

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