namespace EHCTelebot.Services;

public class TelegramWebhookUpdate
{
    public long UpdateId { get; set; }

    public TelegramWebhookMessage? Message { get; set; }
}

public class TelegramWebhookMessage
{
    public long MessageId { get; set; }

    public TelegramWebhookUser? From { get; set; }

    public TelegramWebhookChat? Chat { get; set; }

    public long Date { get; set; }

    public string? Text { get; set; }
}

public class TelegramWebhookUser
{
    public long Id { get; set; }

    public bool IsBot { get; set; }

    public string? FirstName { get; set; }

    public string? Username { get; set; }

    public string? LanguageCode { get; set; }
}

public class TelegramWebhookChat
{
    public long Id { get; set; }

    public string? FirstName { get; set; }

    public string? Username { get; set; }

    public string? Type { get; set; }
}