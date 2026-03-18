using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Clear, "SBQueue", SupportsShouldProcess = true)]
public sealed class ClearSBQueueCommand : SBEntityTargetCmdletBase
{
    [Parameter]
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
            var connectionString = ResolveConnectionString();
            var target = ResolveQueueTarget(Queue);
            var targetText = $"Queue '{target.Queue}' (from {target.Source})";

            if (!ShouldProcess(targetText, "Clear Service Bus queue"))
            {
                return;
            }

            ClearQueueAsync(connectionString, target.Queue).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "ClearSBQueueFailed", ErrorCategory.NotSpecified, Queue));
        }
    }

    private async Task ClearQueueAsync(string connectionString, string queue)
    {
        await using var client = CreateServiceBusClient(connectionString);

        try
        {
            await ClearNonSessionQueueAsync(client, queue);
        }
        catch (InvalidOperationException)
        {
            // Queue likely requires sessions; fall back to session receivers.
            await ClearSessionQueueAsync(client, queue);
        }
    }

    private async Task ClearNonSessionQueueAsync(ServiceBusClient client, string queue)
    {
        await using var receiver = client.CreateReceiver(queue);
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
                try
                {
                    await receiver.CompleteMessageAsync(message);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
                {
                    // Best-effort draining: message is no longer lockable by this receiver.
                }
            }
        }
    }

    private async Task ClearSessionQueueAsync(ServiceBusClient client, string queue)
    {
        while (true)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));

            ServiceBusSessionReceiver? sessionReceiver;
            try
            {
                sessionReceiver = await client.AcceptNextSessionAsync(queue, cancellationToken: cts.Token);
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

                    var sessionLockLost = false;
                    foreach (var message in messages)
                    {
                        try
                        {
                            await sessionReceiver.CompleteMessageAsync(message);
                        }
                        catch (ServiceBusException ex) when (
                            ex.Reason == ServiceBusFailureReason.MessageLockLost ||
                            ex.Reason == ServiceBusFailureReason.SessionLockLost)
                        {
                            // Session lock can expire while draining; continue with next session.
                            sessionLockLost = true;
                            break;
                        }
                    }

                    if (sessionLockLost)
                    {
                        break;
                    }
                }
            }
        }
    }
}
