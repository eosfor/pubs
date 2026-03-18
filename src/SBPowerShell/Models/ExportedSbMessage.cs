using System.Collections.Generic;

namespace SBPowerShell.Models;

public sealed class ExportedSbMessage
{
    public ExportedBrokerProperties BrokerProperties { get; init; } = new();

    public ExportedMessageProperties MessageProperties { get; init; } = new();

    public IReadOnlyDictionary<string, object?> ApplicationProperties { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public ExportedMessageBody Body { get; init; } = new();
}

public sealed class ExportedBrokerProperties
{
    public long SequenceNumber { get; init; }

    public long EnqueuedSequenceNumber { get; init; }

    public DateTimeOffset EnqueuedTimeUtc { get; init; }

    public DateTimeOffset ScheduledEnqueueTimeUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public string? State { get; init; }

    public int DeliveryCount { get; init; }

    public DateTimeOffset LockedUntilUtc { get; init; }

    public string? LockToken { get; init; }

    public string? DeadLetterSource { get; init; }

    public string? DeadLetterReason { get; init; }

    public string? DeadLetterErrorDescription { get; init; }
}

public sealed class ExportedMessageProperties
{
    public string? MessageId { get; init; }

    public string? SessionId { get; init; }

    public string? ReplyToSessionId { get; init; }

    public string? CorrelationId { get; init; }

    public string? Subject { get; init; }

    public string? ContentType { get; init; }

    public string? To { get; init; }

    public string? ReplyTo { get; init; }

    public string? PartitionKey { get; init; }

    public string? TransactionPartitionKey { get; init; }

    public string? ViaPartitionKey { get; init; }

    public string? TimeToLive { get; init; }
}

public sealed class ExportedMessageBody
{
    public int Length { get; init; }

    public string Base64 { get; init; } = string.Empty;

    public string? Utf8 { get; init; }
}
