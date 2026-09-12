namespace EHCTelebot.Models;

public class User
{
    public long ChatId { get; set; }

    public string? Name { get; set; }

    public DateTime? StartDate { get; set; }

    public int ChatState { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastNotificationDate { get; set; }
}