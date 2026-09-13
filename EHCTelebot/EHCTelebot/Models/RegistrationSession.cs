namespace EHCTelebot.Models;

public class RegistrationSession
{
    public string? Name { get; set; }

    public ChatState State { get; set; } = ChatState.WaitingForName;
}