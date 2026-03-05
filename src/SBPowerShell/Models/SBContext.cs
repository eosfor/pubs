namespace SBPowerShell.Models;

public sealed class SBContext
{
    public string? ServiceBusConnectionString { get; init; }
    public string? Queue { get; init; }
    public string? Topic { get; init; }
    public string? Subscription { get; init; }
    public SBTransport? Transport { get; init; }
    public bool IgnoreCertificateChainErrors { get; init; }

    public SBContextEntityMode EntityMode { get; init; } = SBContextEntityMode.Namespace;

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    public string Source { get; init; } = "User";
}
