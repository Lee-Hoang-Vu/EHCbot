using Telegram.Bot;

namespace EHCTelebot.Services;

public class TelegramService
{
    private readonly TelegramBotClient _botClient;

    public TelegramService(IConfiguration configuration)
    {
        var botToken = configuration["Telegram:BotToken"];

        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException(
                "Telegram BotToken is not configured.");
        }

        _botClient = new TelegramBotClient(botToken);
    }

    public TelegramBotClient Client => _botClient;
}