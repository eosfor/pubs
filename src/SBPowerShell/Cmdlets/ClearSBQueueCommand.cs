using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Clear, "SBQueue")]
public sealed class ClearSBQueueCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

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
            ClearQueueAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "ClearSBQueueFailed", ErrorCategory.NotSpecified, Queue));
        }
    }

    private async Task ClearQueueAsync()
    {
        await using var client = new ServiceBusClient(ServiceBusConnectionString);

        try
        {
            await ClearNonSessionQueueAsync(client);
        }
        catch (InvalidOperationException)
        {
            // Queue likely requires sessions; fall back to session receivers.
            await ClearSessionQueueAsync(client);
        }
    }

    private async Task ClearNonSessionQueueAsync(ServiceBusClient client)
    {
        await using var receiver = client.CreateReceiver(Queue);
        var receiveTimeout = TimeSpan.FromSeconds(Math.Max(WaitSeconds, 1) + 5);

        while (true)
        {
            IReadOnlyList<ServiceBusReceivedMessage> messages;
            try
            {
                messages = await receiver
                    .ReceiveMessagesAsync(BatchSize, TimeSpan.FromSeconds(WaitSeconds))
                    .WaitAsync(receiveTimeout);
            }
            catch (TimeoutException)
            {
                break;
            }

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

    private async Task ClearSessionQueueAsync(ServiceBusClient client)
    {
        while (true)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));

            ServiceBusSessionReceiver? sessionReceiver;
            try
            {
                sessionReceiver = await client.AcceptNextSessionAsync(Queue, cancellationToken: cts.Token);
            }
            catch (TaskCanceledException)
            {
                // No more sessions available within wait window.
                break;
            }
        if (sessionReceiver is null)
        {
            break;
        }

        await using (sessionReceiver)
        {
            var receiveTimeout = TimeSpan.FromSeconds(Math.Max(WaitSeconds, 1) + 5);

            while (true)
            {
                IReadOnlyList<ServiceBusReceivedMessage> messages;
                try
                {
                    messages = await sessionReceiver
                        .ReceiveMessagesAsync(BatchSize, TimeSpan.FromSeconds(WaitSeconds))
                        .WaitAsync(receiveTimeout);
                }
                catch (TimeoutException)
                {
                    break;
                }

                if (messages.Count == 0)
                {
                    break;
                }

                    foreach (var message in messages)
                    {
                        await sessionReceiver.CompleteMessageAsync(message);
                    }
                }
            }
        }
    }
}
