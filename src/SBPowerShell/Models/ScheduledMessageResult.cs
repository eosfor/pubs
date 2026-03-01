namespace SBPowerShell.Models;

public sealed class ScheduledMessageResult
{
    public long SequenceNumber { get; init; }

    public string EntityPath { get; init; } = string.Empty;

    public string? SessionId { get; init; }

    public string? MessageId { get; init; }

    public DateTimeOffset ScheduledEnqueueTimeUtc { get; init; }
}
