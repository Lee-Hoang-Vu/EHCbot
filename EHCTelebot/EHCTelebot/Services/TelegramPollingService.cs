using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace EHCTelebot.Services;

public class TelegramPollingService : BackgroundService
{
    private readonly TelegramService _telegramService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramPollingService> _logger;

    public TelegramPollingService(
        TelegramService telegramService,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramPollingService> logger)
    {
        _telegramService = telegramService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var bot = _telegramService.Client;

        try
        {
            var me = await bot.GetMeAsync(stoppingToken);

            _logger.LogInformation(
                "Telegram bot started: @{Username}",
                me.Username);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
                {
                    UpdateType.Message
                },

                // Development:
                // Bỏ qua các message cũ đang chờ khi app khởi động.
                ThrowPendingUpdates = true
            };

            await bot.ReceiveAsync(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Telegram polling stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telegram polling stopped unexpectedly.");
        }
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var handler = scope.ServiceProvider
                .GetRequiredService<TelegramUpdateHandler>();

            await handler.HandleAsync(update);

            _logger.LogInformation(
                "Processed Telegram update {UpdateId}",
                update.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing Telegram update {UpdateId}",
                update.Id);
        }
    }

    private Task HandleErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ApiRequestException apiException)
        {
            _logger.LogError(
                "Telegram API Error {ErrorCode}: {Message}",
                apiException.ErrorCode,
                apiException.Message);
        }
        else
        {
            _logger.LogError(
                exception,
                "Telegram polling error.");
        }

        return Task.CompletedTask;
    }
}