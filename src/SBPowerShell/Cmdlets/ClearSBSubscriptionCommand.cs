using System.Management.Automation;
using System.Threading;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Clear, "SBSubscription")]
public sealed class ClearSBSubscriptionCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
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
            ClearSubscriptionAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "ClearSBSubscriptionFailed", ErrorCategory.NotSpecified, Subscription));
        }
    }

    private async Task ClearSubscriptionAsync()
    {
        await using var client = new ServiceBusClient(ServiceBusConnectionString);

        try
        {
            await using var receiver = client.CreateReceiver(Topic, Subscription);
            await DrainReceiverAsync(receiver);
        }
        catch (InvalidOperationException)
        {
            await ClearSessionSubscriptionAsync(client);
        }
    }

    private async Task ClearSessionSubscriptionAsync(ServiceBusClient client)
    {
        while (true)
        {
            ServiceBusSessionReceiver? sessionReceiver = null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
                sessionReceiver = await client.AcceptNextSessionAsync(Topic, Subscription, cancellationToken: cts.Token);
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
