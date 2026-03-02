using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Reflection;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBSubscription", DefaultParameterSetName = ParameterSetByName)]
[OutputType(typeof(SubscriptionProperties))]
public sealed class GetSBSubscriptionCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetByName = "ByName";
    private const string ParameterSetByTopicObject = "ByTopicObject";

    [Parameter(ParameterSetName = ParameterSetByName, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Alias("TopicName", "Name")]
    public string Topic { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetByTopicObject, Mandatory = true, ValueFromPipeline = true)]
    public TopicProperties? InputObject { get; set; }

    [Parameter(ValueFromPipelineByPropertyName = true)]
    [Alias("SubscriptionName", "SubscriptionMame")]
    public string? Subscription { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            var output = GetSubscriptionsAsync().GetAwaiter().GetResult();
            foreach (var item in output)
            {
                WriteObject(item);
            }
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "GetSBSubscriptionFailed", ErrorCategory.NotSpecified, Subscription ?? Topic ?? (object?)InputObject ?? ServiceBusConnectionString));
        }
    }

    private async Task<IReadOnlyList<object>> GetSubscriptionsAsync()
    {
        var connectionString = ResolveConnectionString();
        var admin = CreateAdminClient(connectionString);
        var (topicName, resolvedSubscription, _) = ResolveTopicWithOptionalSubscription(
            ResolveTopicName(),
            Subscription,
            resolvedConnectionString: connectionString);
        var results = new List<object>();

        if (!string.IsNullOrWhiteSpace(resolvedSubscription))
        {
            var single = await ReadSingleSubscriptionAsync(admin, topicName, resolvedSubscription!, connectionString);
            results.Add(single);
            return results;
        }

        await foreach (var sub in admin.GetSubscriptionsAsync(topicName))
        {
            var runtime = await TryGetRuntimeAsync(admin, topicName, sub.SubscriptionName, connectionString);
            results.Add(BuildSubscriptionObject(sub, runtime));
        }
        return results;
    }

    private string ResolveTopicName()
    {
        if (ParameterSetName == ParameterSetByTopicObject && InputObject is not null)
        {
            return InputObject.Name;
        }

        return Topic;
    }

    private async Task<object> ReadSingleSubscriptionAsync(ServiceBusAdministrationClient admin, string topicName, string subscriptionName, string connectionString)
    {
        SubscriptionProperties subscription;
        try
        {
            subscription = (await admin.GetSubscriptionAsync(topicName, subscriptionName)).Value;
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SubscriptionNotFound", ErrorCategory.ObjectNotFound, $"{topicName}/{subscriptionName}"));
            return new object();
        }

        var runtime = await TryGetRuntimeAsync(admin, topicName, subscriptionName, connectionString);
        return BuildSubscriptionObject(subscription, runtime);
    }

    private async Task<SubscriptionRuntimeProperties?> TryGetRuntimeAsync(ServiceBusAdministrationClient admin, string topicName, string subscriptionName, string connectionString)
    {
        try
        {
            var runtime = (await admin.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName)).Value;
            if (runtime.ActiveMessageCount == 0)
            {
                var active = await TryPeekActiveCountAsync(topicName, subscriptionName, connectionString);
                if (active > 0)
                {
                    SetRuntimeCounts(runtime, active);
                }
            }

            return runtime;
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            return null;
        }
    }

    private async Task<int> TryPeekActiveCountAsync(string topicName, string subscriptionName, string connectionString)
    {
        try
        {
            await using var client = CreateServiceBusClient(connectionString);
            await using var receiver = client.CreateReceiver(topicName, subscriptionName);
            var peeked = await receiver.PeekMessagesAsync(1);
            return peeked.Count;
        }
        catch (ServiceBusException)
        {
            return 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static void SetRuntimeCounts(SubscriptionRuntimeProperties runtime, long activeCount)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var activeProp = typeof(SubscriptionRuntimeProperties).GetProperty(nameof(SubscriptionRuntimeProperties.ActiveMessageCount), flags);
        activeProp?.SetValue(runtime, activeCount);

        var totalProp = typeof(SubscriptionRuntimeProperties).GetProperty(nameof(SubscriptionRuntimeProperties.TotalMessageCount), flags);
        var currentTotal = (long?)(totalProp?.GetValue(runtime) ?? 0);
        if (currentTotal is null || currentTotal < activeCount)
        {
            totalProp?.SetValue(runtime, activeCount);
        }
    }

    private static object BuildSubscriptionObject(SubscriptionProperties subscription, SubscriptionRuntimeProperties? runtime)
    {
        if (runtime is null)
        {
            return subscription;
        }

        var psObj = new PSObject(subscription);
        psObj.Properties.Add(new PSNoteProperty("RuntimeProperties", runtime));
        return psObj;
    }
}
