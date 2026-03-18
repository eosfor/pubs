using System.Management.Automation;
using System.Threading;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Clear, "SBDLQ")]
public sealed class ClearSBDLQCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter TransferDeadLetter { get; set; }

    [Parameter]
    [ValidateRange(1, 1000)]
    public int BatchSize { get; set; } = 50;

    [Parameter]
    [ValidateRange(1, 60)]
    public int WaitSeconds { get; set; } = 1;

    protected override void ProcessRecord()
    {
        try
        {
            var connectionString = ResolveConnectionString();
            var target = ResolveQueueOrSubscriptionTarget(
                Queue,
                Topic,
                Subscription,
                resolvedConnectionString: connectionString);
            ClearAsync(connectionString, target).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "ClearSBDLQFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private async Task ClearAsync(string connectionString, ResolvedEntity target)
    {
        await using var client = CreateServiceBusClient(connectionString);
        var subQueue = ServiceBusSubQueuePath.ResolveSubQueue(TransferDeadLetter);

        if (target.Kind == ResolvedEntityKind.Queue)
        {
            await ClearEntityAsync(client, target.Queue, null, subQueue);
        }
        else
        {
            await ClearEntityAsync(client, target.Topic, target.Subscription, subQueue);
        }
    }

    private async Task ClearEntityAsync(ServiceBusClient client, string entity, string? subscription, SubQueue subQueue)
    {
        try
        {
            await using var receiver = subscription is null
                ? client.CreateReceiver(entity, new ServiceBusReceiverOptions { SubQueue = subQueue })
                : client.CreateReceiver(entity, subscription, new ServiceBusReceiverOptions { SubQueue = subQueue });

            await DrainReceiverAsync(receiver);
        }
        catch (InvalidOperationException)
        {
            await ClearSessionEntityAsync(client, entity, subscription, subQueue);
        }
    }

    private async Task ClearSessionEntityAsync(ServiceBusClient client, string entity, string? subscription, SubQueue subQueue)
    {
        var entityPath = subscription is null
            ? ServiceBusSubQueuePath.BuildQueueEntityPath(entity)
            : ServiceBusSubQueuePath.BuildSubscriptionEntityPath(entity, subscription);

        var sessionPath = ServiceBusSubQueuePath.BuildSessionPath(entityPath, subQueue);

        while (true)
        {
            ServiceBusSessionReceiver? sessionReceiver = null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
                sessionReceiver = await client.AcceptNextSessionAsync(sessionPath, cancellationToken: cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                break;
            }

            if (sessionReceiver is null)
            {
                break;
            }

            await using (sessionReceiver)
            {
                await DrainReceiverAsync(sessionReceiver);
            }
        }
    }

    private async Task DrainReceiverAsync(ServiceBusReceiver receiver)
    {
        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(BatchSize, TimeSpan.FromSeconds(WaitSeconds));
            if (messages.Count == 0)
            {
                break;
            }

            foreach (var message in messages)
            {
                await receiver.CompleteMessageAsync(message);
            }
        }
    }
}
