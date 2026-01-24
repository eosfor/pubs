using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Receive, "SBMessage", DefaultParameterSetName = ParameterSetQueue)]
[OutputType(typeof(ServiceBusReceivedMessage))]
public sealed class ReceiveSBMessageCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";

    private readonly CancellationTokenSource _cts = new();

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int MaxMessages { get; set; }

    [Parameter]
    [ValidateRange(1, 1000)]
    public int BatchSize { get; set; } = 10;

    [Parameter]
    [ValidateRange(1, 300)]
    public int WaitSeconds { get; set; } = 5;

    protected override void EndProcessing()
    {
        try
        {
            Receive(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // user cancellation
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "ReceiveSBMessageFailed", ErrorCategory.NotSpecified, this));
        }
    }

    protected override void StopProcessing()
    {
        _cts.Cancel();
    }

    private void Receive(CancellationToken cancellationToken)
    {
        var client = new ServiceBusClient(ServiceBusConnectionString);

        try
        {
            if (ParameterSetName == ParameterSetQueue)
            {
                ServiceBusReceiver receiver;
                try
                {
                    receiver = client.CreateReceiver(Queue);
                }
                catch (InvalidOperationException)
                {
                    ReceiveFromSessions(client, cancellationToken);
                    return;
                }

                try
                {
                    ReceiveFromReceiver(receiver, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // Queue requires sessions; fall back.
                    ReceiveFromSessions(client, cancellationToken);
                }
                return;
            }

            var subscriptionReceiver = client.CreateReceiver(Topic, Subscription);
            ReceiveFromReceiver(subscriptionReceiver, cancellationToken);
        }
        finally
        {
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void ReceiveFromReceiver(ServiceBusReceiver receiver, CancellationToken cancellationToken)
    {
        try
        {
            var remaining = MaxMessages;

            while (!cancellationToken.IsCancellationRequested)
            {
                var messages = receiver.ReceiveMessagesAsync(
                        BatchSize,
                        TimeSpan.FromSeconds(WaitSeconds),
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult();

                if (messages.Count == 0)
                {
                    if (MaxMessages > 0 && remaining <= 0)
                    {
                        break;
                    }

                    continue;
                }

                foreach (var message in messages)
                {
                    WriteObject(message);
                    receiver.CompleteMessageAsync(message, cancellationToken)
                        .GetAwaiter()
                        .GetResult();

                    if (MaxMessages > 0)
                    {
                        remaining--;
                        if (remaining <= 0)
                        {
                            return;
                        }
                    }
                }
            }
        }
        finally
        {
            receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void ReceiveFromSessions(ServiceBusClient client, CancellationToken cancellationToken)
    {
        var remaining = MaxMessages;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));

            ServiceBusSessionReceiver? sessionReceiver;
            try
            {
                sessionReceiver = client.AcceptNextSessionAsync(Queue, cancellationToken: cts.Token)
                    .GetAwaiter()
                    .GetResult();
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

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var messages = sessionReceiver.ReceiveMessagesAsync(
                            BatchSize,
                            TimeSpan.FromSeconds(WaitSeconds),
                            cancellationToken)
                        .GetAwaiter()
                        .GetResult();

                    if (messages.Count == 0)
                    {
                        break;
                    }

                    foreach (var message in messages)
                    {
                        WriteObject(message);
                        sessionReceiver.CompleteMessageAsync(message, cancellationToken)
                            .GetAwaiter()
                            .GetResult();

                        if (MaxMessages > 0)
                        {
                            remaining--;
                            if (remaining <= 0)
                            {
                                return;
                            }
                        }
                    }
                }
            }
            finally
            {
                sessionReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
