namespace SBPowerShell.Models;

public sealed class TopologySnapshot
{
    public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<QueueSnapshot> Queues { get; set; } = [];

    public List<TopicSnapshot> Topics { get; set; } = [];
}

public sealed class QueueSnapshot
{
    public string Name { get; set; } = string.Empty;
    public bool RequiresSession { get; set; }
    public bool RequiresDuplicateDetection { get; set; }
    public bool EnablePartitioning { get; set; }
    public bool EnableBatchedOperations { get; set; }
    public bool DeadLetteringOnMessageExpiration { get; set; }
    public int MaxDeliveryCount { get; set; }
    public long MaxSizeInMegabytes { get; set; }
    public long? MaxMessageSizeInKilobytes { get; set; }
    public string? LockDuration { get; set; }
    public string? DefaultMessageTimeToLive { get; set; }
    public string? AutoDeleteOnIdle { get; set; }
    public string? DuplicateDetectionHistoryTimeWindow { get; set; }
    public string? ForwardTo { get; set; }
    public string? ForwardDeadLetteredMessagesTo { get; set; }
    public string? UserMetadata { get; set; }
}

public sealed class TopicSnapshot
{
    public string Name { get; set; } = string.Empty;
    public bool RequiresDuplicateDetection { get; set; }
    public bool EnablePartitioning { get; set; }
    public bool EnableBatchedOperations { get; set; }
    public bool SupportOrdering { get; set; }
    public long MaxSizeInMegabytes { get; set; }
    public long? MaxMessageSizeInKilobytes { get; set; }
    public string? DefaultMessageTimeToLive { get; set; }
    public string? AutoDeleteOnIdle { get; set; }
    public string? DuplicateDetectionHistoryTimeWindow { get; set; }
    public string? UserMetadata { get; set; }

    public List<SubscriptionSnapshot> Subscriptions { get; set; } = [];
}

public sealed class SubscriptionSnapshot
{
    public string Name { get; set; } = string.Empty;
    public bool RequiresSession { get; set; }
    public bool EnableBatchedOperations { get; set; }
    public bool DeadLetteringOnMessageExpiration { get; set; }
    public bool EnableDeadLetteringOnFilterEvaluationExceptions { get; set; }
    public int MaxDeliveryCount { get; set; }
    public string? LockDuration { get; set; }
    public string? DefaultMessageTimeToLive { get; set; }
    public string? AutoDeleteOnIdle { get; set; }
    public string? ForwardTo { get; set; }
    public string? ForwardDeadLetteredMessagesTo { get; set; }
    public string? UserMetadata { get; set; }

    public List<RuleSnapshot> Rules { get; set; } = [];
}

public sealed class RuleSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string FilterType { get; set; } = "True";
    public string? SqlFilter { get; set; }
    public string? SqlAction { get; set; }
    public CorrelationSnapshot? Correlation { get; set; }
}

public sealed class CorrelationSnapshot
{
    public string? CorrelationId { get; set; }
    public string? MessageId { get; set; }
    public string? To { get; set; }
    public string? ReplyTo { get; set; }
    public string? Subject { get; set; }
    public string? SessionId { get; set; }
    public string? ReplyToSessionId { get; set; }
    public string? ContentType { get; set; }
    public Dictionary<string, string> ApplicationProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
