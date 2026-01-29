using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Receive, "SBDLQMessage", DefaultParameterSetName = ParameterSetQueue)]
[OutputType(typeof(ServiceBusReceivedMessage))]
public sealed class ReceiveSBDLQMessageCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";

    private readonly CancellationTokenSource _cts = new();

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
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

    [Parameter]
    public SwitchParameter Peek { get; set; }

    [Parameter]
    public SwitchParameter NoComplete { get; set; }

    protected override void EndProcessing()
    {
        try
        {
            Receive(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // user cancelled
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "ReceiveSBDLQMessageFailed", ErrorCategory.NotSpecified, this));
        }
    }

    protected override void StopProcessing()
    {
        _cts.Cancel();
    }

    private void Receive(CancellationToken cancellationToken)
    {
        ServiceBusClient? client = null;
        try
        {
            client = new ServiceBusClient(ServiceBusConnectionString);

            if (ParameterSetName == ParameterSetQueue)
            {
                ReceiveQueue(client, cancellationToken);
            }
            else
            {
                ReceiveSubscription(client, cancellationToken);
            }
        }
        finally
        {
            client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void ReceiveQueue(ServiceBusClient client, CancellationToken cancellationToken)
    {
        try
        {
            var receiver = client.CreateReceiver(
                Queue,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            ReceiveFromReceiver(receiver, Peek, NoComplete, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Queue requires sessions; switch to session-aware receiver.
            ReceiveFromDeadLetterSessions(client, Queue, cancellationToken);
        }
    }

    private void ReceiveSubscription(ServiceBusClient client, CancellationToken cancellationToken)
    {
        try
        {
            var receiver = client.CreateReceiver(
                Topic,
                Subscription,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            ReceiveFromReceiver(receiver, Peek, NoComplete, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Subscription requires sessions; switch to session-aware receiver.
            var entityPath = $"{Topic}/Subscriptions/{Subscription}";
            ReceiveFromDeadLetterSessions(client, entityPath, cancellationToken);
        }
    }

    private void ReceiveFromReceiver(ServiceBusReceiver receiver, bool peek, bool noComplete, CancellationToken cancellationToken, bool disposeReceiver = true)
    {
        try
        {
            var remaining = MaxMessages;

            while (!cancellationToken.IsCancellationRequested)
            {
                IReadOnlyList<ServiceBusReceivedMessage> messages;

                if (peek)
                {
                    messages = receiver.PeekMessagesAsync(
                            BatchSize,
                            cancellationToken: cancellationToken)
                        .GetAwaiter()
                        .GetResult();
                }
                else
                {
                    messages = receiver.ReceiveMessagesAsync(
                            BatchSize,
                            TimeSpan.FromSeconds(WaitSeconds),
                            cancellationToken)
                        .GetAwaiter()
                        .GetResult();
                }

                if (messages.Count == 0)
                {
                    if (!peek)
                    {
                        if (MaxMessages > 0)
                        {
                            break;
                        }

                        continue;
                    }

                    Task.Delay(TimeSpan.FromSeconds(WaitSeconds), cancellationToken)
                        .GetAwaiter()
                        .GetResult();
                    continue;
                }

                foreach (var message in messages)
                {
                    WriteObject(message);

                    if (!peek && !noComplete)
                    {
                        receiver.CompleteMessageAsync(message, cancellationToken)
                            .GetAwaiter()
                            .GetResult();
                    }

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
            if (disposeReceiver)
            {
                receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    private void ReceiveFromDeadLetterSessions(ServiceBusClient client, string entityPath, CancellationToken cancellationToken)
    {
        var remaining = MaxMessages;
        var deadLetterPath = $"{entityPath}/$DeadLetterQueue";

        while (!cancellationToken.IsCancellationRequested)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));

            ServiceBusSessionReceiver? sessionReceiver;
            try
            {
                sessionReceiver = client.AcceptNextSessionAsync(deadLetterPath, cancellationToken: cts.Token)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (TaskCanceledException)
            {
                // No more sessions within wait window.
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
                    IReadOnlyList<ServiceBusReceivedMessage> messages;

                    if (Peek)
                    {
                        messages = sessionReceiver.PeekMessagesAsync(
                                BatchSize,
                                cancellationToken: cancellationToken)
                            .GetAwaiter()
                            .GetResult();
                    }
                    else
                    {
                        messages = sessionReceiver.ReceiveMessagesAsync(
                                BatchSize,
                                TimeSpan.FromSeconds(WaitSeconds),
                                cancellationToken)
                            .GetAwaiter()
                            .GetResult();
                    }

                    if (messages.Count == 0)
                    {
                        if (Peek)
                        {
                            Task.Delay(TimeSpan.FromSeconds(WaitSeconds), cancellationToken)
                                .GetAwaiter()
                                .GetResult();
                        }
                        else
                        {
                            break;
                        }
                    }

                    foreach (var message in messages)
                    {
                        WriteObject(message);

                        if (!Peek && !NoComplete)
                        {
                            sessionReceiver.CompleteMessageAsync(message, cancellationToken)
                                .GetAwaiter()
                                .GetResult();
                        }

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
