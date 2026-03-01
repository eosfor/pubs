using System.Text.Json;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell.Models;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsData.Import, "SBTopology", SupportsShouldProcess = true)]
public sealed class ImportSBTopologyCommand : PSCmdlet
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    [Parameter]
    [ValidateSet("Upsert", "CreateOnly")]
    public string Mode { get; set; } = "Upsert";

    protected override void ProcessRecord()
    {
        try
        {
            var fullPath = System.IO.Path.GetFullPath(Path);
            var json = File.ReadAllText(fullPath);
            var snapshot = JsonSerializer.Deserialize<TopologySnapshot>(json, JsonOptions)
                ?? throw new InvalidOperationException("Topology file is empty or invalid.");

            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);

            ImportQueues(admin, snapshot.Queues);
            ImportTopics(admin, snapshot.Topics);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "ImportSBTopologyFailed", ErrorCategory.NotSpecified, Path));
        }
    }

    private void ImportQueues(ServiceBusAdministrationClient admin, IReadOnlyCollection<QueueSnapshot> queues)
    {
        foreach (var queue in queues)
        {
            var exists = admin.QueueExistsAsync(queue.Name).GetAwaiter().GetResult();
            if (!exists)
            {
                if (ShouldProcess(queue.Name, "Create queue"))
                {
                    var options = new CreateQueueOptions(queue.Name);
                    ApplyCreateQueue(options, queue);
                    admin.CreateQueueAsync(options).GetAwaiter().GetResult();
                }
                continue;
            }

            if (Mode == "CreateOnly")
            {
                continue;
            }

            if (ShouldProcess(queue.Name, "Update queue"))
            {
                var current = admin.GetQueueAsync(queue.Name).GetAwaiter().GetResult().Value;
                ApplyUpdateQueue(current, queue);
                admin.UpdateQueueAsync(current).GetAwaiter().GetResult();
            }
        }
    }

    private void ImportTopics(ServiceBusAdministrationClient admin, IReadOnlyCollection<TopicSnapshot> topics)
    {
        foreach (var topic in topics)
        {
            var exists = admin.TopicExistsAsync(topic.Name).GetAwaiter().GetResult();
            if (!exists)
            {
                if (ShouldProcess(topic.Name, "Create topic"))
                {
                    var options = new CreateTopicOptions(topic.Name);
                    ApplyCreateTopic(options, topic);
                    admin.CreateTopicAsync(options).GetAwaiter().GetResult();
                }
            }
            else if (Mode == "Upsert")
            {
                if (ShouldProcess(topic.Name, "Update topic"))
                {
                    var current = admin.GetTopicAsync(topic.Name).GetAwaiter().GetResult().Value;
                    ApplyUpdateTopic(current, topic);
                    admin.UpdateTopicAsync(current).GetAwaiter().GetResult();
                }
            }

            ImportSubscriptions(admin, topic.Name, topic.Subscriptions);
        }
    }

    private void ImportSubscriptions(ServiceBusAdministrationClient admin, string topic, IReadOnlyCollection<SubscriptionSnapshot> subscriptions)
    {
        foreach (var sub in subscriptions)
        {
            var exists = admin.SubscriptionExistsAsync(topic, sub.Name).GetAwaiter().GetResult();
            if (!exists)
            {
                if (ShouldProcess($"{topic}/{sub.Name}", "Create subscription"))
                {
                    var options = new CreateSubscriptionOptions(topic, sub.Name);
                    ApplyCreateSubscription(options, sub);
                    admin.CreateSubscriptionAsync(options).GetAwaiter().GetResult();
                }
            }
            else if (Mode == "Upsert")
            {
                if (ShouldProcess($"{topic}/{sub.Name}", "Update subscription"))
                {
                    var current = admin.GetSubscriptionAsync(topic, sub.Name).GetAwaiter().GetResult().Value;
                    ApplyUpdateSubscription(current, sub);
                    admin.UpdateSubscriptionAsync(current).GetAwaiter().GetResult();
                }
            }

            ImportRules(admin, topic, sub.Name, sub.Rules);
        }
    }

    private void ImportRules(ServiceBusAdministrationClient admin, string topic, string subscription, IReadOnlyCollection<RuleSnapshot> rules)
    {
        foreach (var rule in rules)
        {
            var exists = admin.RuleExistsAsync(topic, subscription, rule.Name).GetAwaiter().GetResult();
            if (!exists)
            {
                if (ShouldProcess($"{topic}/{subscription}/{rule.Name}", "Create rule"))
                {
                    var options = new CreateRuleOptions(rule.Name)
                    {
                        Filter = BuildFilter(rule),
                        Action = BuildAction(rule)
                    };
                    admin.CreateRuleAsync(topic, subscription, options).GetAwaiter().GetResult();
                }
                continue;
            }

            if (Mode == "CreateOnly")
            {
                continue;
            }

            if (ShouldProcess($"{topic}/{subscription}/{rule.Name}", "Update rule"))
            {
                var current = admin.GetRuleAsync(topic, subscription, rule.Name).GetAwaiter().GetResult().Value;
                current.Filter = BuildFilter(rule);
                current.Action = BuildAction(rule);
                admin.UpdateRuleAsync(topic, subscription, current).GetAwaiter().GetResult();
            }
        }
    }

    private static void ApplyCreateQueue(CreateQueueOptions options, QueueSnapshot snapshot)
    {
        options.RequiresSession = snapshot.RequiresSession;
        options.RequiresDuplicateDetection = snapshot.RequiresDuplicateDetection;
        options.EnablePartitioning = snapshot.EnablePartitioning;
        options.EnableBatchedOperations = snapshot.EnableBatchedOperations;
        options.DeadLetteringOnMessageExpiration = snapshot.DeadLetteringOnMessageExpiration;
        options.MaxDeliveryCount = snapshot.MaxDeliveryCount;
        options.MaxSizeInMegabytes = snapshot.MaxSizeInMegabytes;
        if (snapshot.MaxMessageSizeInKilobytes.HasValue) options.MaxMessageSizeInKilobytes = snapshot.MaxMessageSizeInKilobytes.Value;

        ApplyTimeSpan(snapshot.LockDuration, t => options.LockDuration = t);
        ApplyTimeSpan(snapshot.DefaultMessageTimeToLive, t => options.DefaultMessageTimeToLive = t);
        ApplyTimeSpan(snapshot.AutoDeleteOnIdle, t => options.AutoDeleteOnIdle = t);
        ApplyTimeSpan(snapshot.DuplicateDetectionHistoryTimeWindow, t => options.DuplicateDetectionHistoryTimeWindow = t);

        if (snapshot.ForwardTo is not null) options.ForwardTo = snapshot.ForwardTo;
        if (snapshot.ForwardDeadLetteredMessagesTo is not null) options.ForwardDeadLetteredMessagesTo = snapshot.ForwardDeadLetteredMessagesTo;
        if (snapshot.UserMetadata is not null) options.UserMetadata = snapshot.UserMetadata;
    }

    private static void ApplyUpdateQueue(QueueProperties queue, QueueSnapshot snapshot)
    {
        queue.EnableBatchedOperations = snapshot.EnableBatchedOperations;
        queue.DeadLetteringOnMessageExpiration = snapshot.DeadLetteringOnMessageExpiration;
        queue.MaxDeliveryCount = snapshot.MaxDeliveryCount;
        queue.MaxSizeInMegabytes = snapshot.MaxSizeInMegabytes;
        if (snapshot.MaxMessageSizeInKilobytes.HasValue) queue.MaxMessageSizeInKilobytes = snapshot.MaxMessageSizeInKilobytes.Value;

        ApplyTimeSpan(snapshot.LockDuration, t => queue.LockDuration = t);
        ApplyTimeSpan(snapshot.DefaultMessageTimeToLive, t => queue.DefaultMessageTimeToLive = t);
        ApplyTimeSpan(snapshot.AutoDeleteOnIdle, t => queue.AutoDeleteOnIdle = t);
        ApplyTimeSpan(snapshot.DuplicateDetectionHistoryTimeWindow, t => queue.DuplicateDetectionHistoryTimeWindow = t);

        if (snapshot.ForwardTo is not null) queue.ForwardTo = snapshot.ForwardTo;
        if (snapshot.ForwardDeadLetteredMessagesTo is not null) queue.ForwardDeadLetteredMessagesTo = snapshot.ForwardDeadLetteredMessagesTo;
        if (snapshot.UserMetadata is not null) queue.UserMetadata = snapshot.UserMetadata;
    }

    private static void ApplyCreateTopic(CreateTopicOptions options, TopicSnapshot snapshot)
    {
        options.RequiresDuplicateDetection = snapshot.RequiresDuplicateDetection;
        options.EnablePartitioning = snapshot.EnablePartitioning;
        options.EnableBatchedOperations = snapshot.EnableBatchedOperations;
        options.SupportOrdering = snapshot.SupportOrdering;
        options.MaxSizeInMegabytes = snapshot.MaxSizeInMegabytes;
        if (snapshot.MaxMessageSizeInKilobytes.HasValue) options.MaxMessageSizeInKilobytes = snapshot.MaxMessageSizeInKilobytes.Value;

        ApplyTimeSpan(snapshot.DefaultMessageTimeToLive, t => options.DefaultMessageTimeToLive = t);
        ApplyTimeSpan(snapshot.AutoDeleteOnIdle, t => options.AutoDeleteOnIdle = t);
        ApplyTimeSpan(snapshot.DuplicateDetectionHistoryTimeWindow, t => options.DuplicateDetectionHistoryTimeWindow = t);

        if (snapshot.UserMetadata is not null) options.UserMetadata = snapshot.UserMetadata;
    }

    private static void ApplyUpdateTopic(TopicProperties topic, TopicSnapshot snapshot)
    {
        topic.EnableBatchedOperations = snapshot.EnableBatchedOperations;
        topic.MaxSizeInMegabytes = snapshot.MaxSizeInMegabytes;
        if (snapshot.MaxMessageSizeInKilobytes.HasValue) topic.MaxMessageSizeInKilobytes = snapshot.MaxMessageSizeInKilobytes.Value;

        ApplyTimeSpan(snapshot.DefaultMessageTimeToLive, t => topic.DefaultMessageTimeToLive = t);
        ApplyTimeSpan(snapshot.AutoDeleteOnIdle, t => topic.AutoDeleteOnIdle = t);
        ApplyTimeSpan(snapshot.DuplicateDetectionHistoryTimeWindow, t => topic.DuplicateDetectionHistoryTimeWindow = t);

        if (snapshot.UserMetadata is not null) topic.UserMetadata = snapshot.UserMetadata;
    }

    private static void ApplyCreateSubscription(CreateSubscriptionOptions options, SubscriptionSnapshot snapshot)
    {
        options.RequiresSession = snapshot.RequiresSession;
        options.EnableBatchedOperations = snapshot.EnableBatchedOperations;
        options.DeadLetteringOnMessageExpiration = snapshot.DeadLetteringOnMessageExpiration;
        options.EnableDeadLetteringOnFilterEvaluationExceptions = snapshot.EnableDeadLetteringOnFilterEvaluationExceptions;
        options.MaxDeliveryCount = snapshot.MaxDeliveryCount;

        ApplyTimeSpan(snapshot.LockDuration, t => options.LockDuration = t);
        ApplyTimeSpan(snapshot.DefaultMessageTimeToLive, t => options.DefaultMessageTimeToLive = t);
        ApplyTimeSpan(snapshot.AutoDeleteOnIdle, t => options.AutoDeleteOnIdle = t);

        if (snapshot.ForwardTo is not null) options.ForwardTo = snapshot.ForwardTo;
        if (snapshot.ForwardDeadLetteredMessagesTo is not null) options.ForwardDeadLetteredMessagesTo = snapshot.ForwardDeadLetteredMessagesTo;
        if (snapshot.UserMetadata is not null) options.UserMetadata = snapshot.UserMetadata;
    }

    private static void ApplyUpdateSubscription(SubscriptionProperties subscription, SubscriptionSnapshot snapshot)
    {
        subscription.EnableBatchedOperations = snapshot.EnableBatchedOperations;
        subscription.DeadLetteringOnMessageExpiration = snapshot.DeadLetteringOnMessageExpiration;
        subscription.EnableDeadLetteringOnFilterEvaluationExceptions = snapshot.EnableDeadLetteringOnFilterEvaluationExceptions;
        subscription.MaxDeliveryCount = snapshot.MaxDeliveryCount;

        ApplyTimeSpan(snapshot.LockDuration, t => subscription.LockDuration = t);
        ApplyTimeSpan(snapshot.DefaultMessageTimeToLive, t => subscription.DefaultMessageTimeToLive = t);
        ApplyTimeSpan(snapshot.AutoDeleteOnIdle, t => subscription.AutoDeleteOnIdle = t);

        if (snapshot.ForwardTo is not null) subscription.ForwardTo = snapshot.ForwardTo;
        if (snapshot.ForwardDeadLetteredMessagesTo is not null) subscription.ForwardDeadLetteredMessagesTo = snapshot.ForwardDeadLetteredMessagesTo;
        if (snapshot.UserMetadata is not null) subscription.UserMetadata = snapshot.UserMetadata;
    }

    private static RuleFilter BuildFilter(RuleSnapshot rule)
    {
        return rule.FilterType switch
        {
            "Sql" => new SqlRuleFilter(rule.SqlFilter ?? "1=1"),
            "Correlation" => BuildCorrelationFilter(rule.Correlation),
            "False" => new FalseRuleFilter(),
            _ => new TrueRuleFilter()
        };
    }

    private static RuleAction? BuildAction(RuleSnapshot rule)
    {
        return string.IsNullOrWhiteSpace(rule.SqlAction)
            ? null
            : new SqlRuleAction(rule.SqlAction);
    }

    private static CorrelationRuleFilter BuildCorrelationFilter(CorrelationSnapshot? snapshot)
    {
        var filter = new CorrelationRuleFilter();
        if (snapshot is null)
        {
            return filter;
        }

        filter.CorrelationId = snapshot.CorrelationId;
        filter.MessageId = snapshot.MessageId;
        filter.To = snapshot.To;
        filter.ReplyTo = snapshot.ReplyTo;
        filter.Subject = snapshot.Subject;
        filter.SessionId = snapshot.SessionId;
        filter.ReplyToSessionId = snapshot.ReplyToSessionId;
        filter.ContentType = snapshot.ContentType;

        foreach (var property in snapshot.ApplicationProperties)
        {
            filter.ApplicationProperties[property.Key] = property.Value;
        }

        return filter;
    }

    private static void ApplyTimeSpan(string? value, Action<TimeSpan> apply)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (TimeSpan.TryParse(value, out var parsed))
        {
            apply(parsed);
        }
    }
}
