namespace EHCTelebot.Models;

public class User
{
    public long ChatId { get; set; }

    public string? Name { get; set; }

    public DateTime? StartDate { get; set; }

    public int ChatState { get; set; } = 2;

    public bool IsActive { get; set; } = true;

    public DateTime? LastNotificationDate { get; set; }
}