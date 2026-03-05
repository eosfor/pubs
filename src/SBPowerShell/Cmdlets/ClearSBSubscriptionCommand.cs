using System.Management.Automation;
using System.Threading;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Clear, "SBSubscription")]
public sealed class ClearSBSubscriptionCommand : SBEntityTargetCmdletBase
{
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

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
            var target = ResolveSubscriptionTarget(Topic, Subscription, resolvedConnectionString: connectionString);
            ClearSubscriptionAsync(connectionString, target.Topic, target.Subscription).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "ClearSBSubscriptionFailed", ErrorCategory.NotSpecified, Subscription));
        }
    }

    private async Task ClearSubscriptionAsync(string connectionString, string topic, string subscription)
    {
        await using var client = CreateServiceBusClient(connectionString);

        try
        {
            await using var receiver = client.CreateReceiver(topic, subscription);
            await DrainReceiverAsync(receiver);
        }
        catch (InvalidOperationException)
        {
            await ClearSessionSubscriptionAsync(client, topic, subscription);
        }
    }

    private async Task ClearSessionSubscriptionAsync(ServiceBusClient client, string topic, string subscription)
    {
        while (true)
        {
            ServiceBusSessionReceiver? sessionReceiver = null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
                sessionReceiver = await client.AcceptNextSessionAsync(topic, subscription, cancellationToken: cts.Token);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                // no more sessions available
                break;
            }
            catch (TaskCanceledException)
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
            IReadOnlyList<ServiceBusReceivedMessage> messages;
            try
            {
                messages = await receiver.ReceiveMessagesAsync(BatchSize, TimeSpan.FromSeconds(WaitSeconds));
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.SessionLockLost)
            {
                return;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceCommunicationProblem)
            {
                // Connection reset while draining; treat as best-effort completion.
                return;
            }

            if (messages.Count == 0)
            {
                break;
            }

            foreach (var message in messages)
            {
                try
                {
                    await receiver.CompleteMessageAsync(message);
                }
                catch (ServiceBusException ex) when (
                    ex.Reason == ServiceBusFailureReason.MessageLockLost ||
                    ex.Reason == ServiceBusFailureReason.SessionLockLost ||
                    ex.Reason == ServiceBusFailureReason.ServiceCommunicationProblem)
                {
                    // Best-effort drain for lock-based entities.
                    return;
                }
            }
        }
    }
}
