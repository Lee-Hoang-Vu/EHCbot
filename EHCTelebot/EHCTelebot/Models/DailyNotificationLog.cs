namespace EHCTelebot.Models;

public class DailyNotificationLog
{
    public long Id { get; set; }

    public long ChatId { get; set; }

    public DateTime NotificationDate { get; set; }

    public string Status { get; set; } = "Failed";

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public string? ErrorMessage { get; set; }
}