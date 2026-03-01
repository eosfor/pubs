using System.Text.Json;
using System.Text.Json.Serialization;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell.Models;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsData.Export, "SBTopology", SupportsShouldProcess = true)]
[OutputType(typeof(string))]
public sealed class ExportSBTopologyCommand : PSCmdlet
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {
        if (!ShouldProcess(Path, "Export Service Bus topology"))
        {
            return;
        }

        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var snapshot = BuildSnapshot(admin);

            var fullPath = System.IO.Path.GetFullPath(Path);
            var dir = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(fullPath, json);

            WriteObject(fullPath);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "ExportSBTopologyFailed", ErrorCategory.NotSpecified, Path));
        }
    }

    private static TopologySnapshot BuildSnapshot(ServiceBusAdministrationClient admin)
    {
        var snapshot = new TopologySnapshot();

        snapshot.Queues = ReadQueues(admin).GetAwaiter().GetResult();
        snapshot.Topics = ReadTopics(admin).GetAwaiter().GetResult();

        return snapshot;
    }

    private static async Task<List<QueueSnapshot>> ReadQueues(ServiceBusAdministrationClient admin)
    {
        var queues = new List<QueueSnapshot>();
        await foreach (var queue in admin.GetQueuesAsync())
        {
            queues.Add(new QueueSnapshot
            {
                Name = queue.Name,
                RequiresSession = queue.RequiresSession,
                RequiresDuplicateDetection = queue.RequiresDuplicateDetection,
                EnablePartitioning = queue.EnablePartitioning,
                EnableBatchedOperations = queue.EnableBatchedOperations,
                DeadLetteringOnMessageExpiration = queue.DeadLetteringOnMessageExpiration,
                MaxDeliveryCount = queue.MaxDeliveryCount,
                MaxSizeInMegabytes = queue.MaxSizeInMegabytes,
                MaxMessageSizeInKilobytes = queue.MaxMessageSizeInKilobytes,
                LockDuration = queue.LockDuration.ToString("c"),
                DefaultMessageTimeToLive = queue.DefaultMessageTimeToLive.ToString("c"),
                AutoDeleteOnIdle = queue.AutoDeleteOnIdle.ToString("c"),
                DuplicateDetectionHistoryTimeWindow = queue.DuplicateDetectionHistoryTimeWindow.ToString("c"),
                ForwardTo = queue.ForwardTo,
                ForwardDeadLetteredMessagesTo = queue.ForwardDeadLetteredMessagesTo,
                UserMetadata = queue.UserMetadata
            });
        }

        return queues.OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<List<TopicSnapshot>> ReadTopics(ServiceBusAdministrationClient admin)
    {
        var topics = new List<TopicSnapshot>();

        await foreach (var topic in admin.GetTopicsAsync())
        {
            var snapshot = new TopicSnapshot
            {
                Name = topic.Name,
                RequiresDuplicateDetection = topic.RequiresDuplicateDetection,
                EnablePartitioning = topic.EnablePartitioning,
                EnableBatchedOperations = topic.EnableBatchedOperations,
                SupportOrdering = topic.SupportOrdering,
                MaxSizeInMegabytes = topic.MaxSizeInMegabytes,
                MaxMessageSizeInKilobytes = topic.MaxMessageSizeInKilobytes,
                DefaultMessageTimeToLive = topic.DefaultMessageTimeToLive.ToString("c"),
                AutoDeleteOnIdle = topic.AutoDeleteOnIdle.ToString("c"),
                DuplicateDetectionHistoryTimeWindow = topic.DuplicateDetectionHistoryTimeWindow.ToString("c"),
                UserMetadata = topic.UserMetadata,
                Subscriptions = await ReadSubscriptions(admin, topic.Name)
            };

            topics.Add(snapshot);
        }

        return topics.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<List<SubscriptionSnapshot>> ReadSubscriptions(ServiceBusAdministrationClient admin, string topic)
    {
        var subscriptions = new List<SubscriptionSnapshot>();

        await foreach (var sub in admin.GetSubscriptionsAsync(topic))
        {
            var snapshot = new SubscriptionSnapshot
            {
                Name = sub.SubscriptionName,
                RequiresSession = sub.RequiresSession,
                EnableBatchedOperations = sub.EnableBatchedOperations,
                DeadLetteringOnMessageExpiration = sub.DeadLetteringOnMessageExpiration,
                EnableDeadLetteringOnFilterEvaluationExceptions = sub.EnableDeadLetteringOnFilterEvaluationExceptions,
                MaxDeliveryCount = sub.MaxDeliveryCount,
                LockDuration = sub.LockDuration.ToString("c"),
                DefaultMessageTimeToLive = sub.DefaultMessageTimeToLive.ToString("c"),
                AutoDeleteOnIdle = sub.AutoDeleteOnIdle.ToString("c"),
                ForwardTo = sub.ForwardTo,
                ForwardDeadLetteredMessagesTo = sub.ForwardDeadLetteredMessagesTo,
                UserMetadata = sub.UserMetadata,
                Rules = await ReadRules(admin, topic, sub.SubscriptionName)
            };

            subscriptions.Add(snapshot);
        }

        return subscriptions.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<List<RuleSnapshot>> ReadRules(ServiceBusAdministrationClient admin, string topic, string subscription)
    {
        var rules = new List<RuleSnapshot>();

        await foreach (var rule in admin.GetRulesAsync(topic, subscription))
        {
            rules.Add(ToRuleSnapshot(rule));
        }

        return rules.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static RuleSnapshot ToRuleSnapshot(RuleProperties rule)
    {
        var snapshot = new RuleSnapshot
        {
            Name = rule.Name,
            SqlAction = (rule.Action as SqlRuleAction)?.SqlExpression
        };

        switch (rule.Filter)
        {
            case TrueRuleFilter:
                snapshot.FilterType = "True";
                break;
            case FalseRuleFilter:
                snapshot.FilterType = "False";
                break;
            case CorrelationRuleFilter correlation:
                snapshot.FilterType = "Correlation";
                snapshot.Correlation = new CorrelationSnapshot
                {
                    CorrelationId = correlation.CorrelationId,
                    MessageId = correlation.MessageId,
                    To = correlation.To,
                    ReplyTo = correlation.ReplyTo,
                    Subject = correlation.Subject,
                    SessionId = correlation.SessionId,
                    ReplyToSessionId = correlation.ReplyToSessionId,
                    ContentType = correlation.ContentType,
                    ApplicationProperties = correlation.ApplicationProperties
                        .ToDictionary(k => k.Key, v => Convert.ToString(v.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                };
                break;
            case SqlRuleFilter sql:
                snapshot.FilterType = "Sql";
                snapshot.SqlFilter = sql.SqlExpression;
                break;
            default:
                snapshot.FilterType = "True";
                break;
        }

        return snapshot;
    }
}
