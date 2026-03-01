namespace SBPowerShell.Models;

public sealed class SBSessionInfo
{
    public string SessionId { get; init; } = string.Empty;

    public string EntityPath { get; init; } = string.Empty;

    public string? Queue { get; init; }

    public string? Topic { get; init; }

    public string? Subscription { get; init; }
}
