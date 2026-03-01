using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Receive, "SBMessage", DefaultParameterSetName = ParameterSetQueue)]
[OutputType(typeof(ServiceBusReceivedMessage))]
public sealed class ReceiveSBMessageCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetQueueMax = "QueueMax";
    private const string ParameterSetQueueWait = "QueueWait";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetSubscriptionMax = "SubscriptionMax";
    private const string ParameterSetSubscriptionWait = "SubscriptionWait";
    private const string ParameterSetContext = "Context";
    private const string ParameterSetContextMax = "ContextMax";
    private const string ParameterSetContextWait = "ContextWait";

    private static readonly TimeSpan DefaultReceiveWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);

    private readonly CancellationTokenSource _cts = new();

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetQueueMax)]
    [Parameter(ParameterSetName = ParameterSetQueueWait)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueueMax)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueueWait)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscriptionWait)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscriptionWait)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueueMax)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContextMax)]
    [ValidateRange(1, int.MaxValue)]
    public int MaxMessages { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetQueueMax)]
    [Parameter(ParameterSetName = ParameterSetQueueWait)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [Parameter(ParameterSetName = ParameterSetContextMax)]
    [Parameter(ParameterSetName = ParameterSetContextWait)]
    [ValidateRange(1, 1000)]
    public int BatchSize { get; set; } = 10;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueueWait)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscriptionWait)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContextWait)]
    [ValidateRange(1, 300)]
    public int WaitSeconds { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetQueueMax)]
    [Parameter(ParameterSetName = ParameterSetQueueWait)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [Parameter(ParameterSetName = ParameterSetContextMax)]
    [Parameter(ParameterSetName = ParameterSetContextWait)]
    public SwitchParameter Peek { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetQueueMax)]
    [Parameter(ParameterSetName = ParameterSetQueueWait)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [Parameter(ParameterSetName = ParameterSetContextMax)]
    [Parameter(ParameterSetName = ParameterSetContextWait)]
    public SwitchParameter NoComplete { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContext, ValueFromPipeline = true)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContextMax, ValueFromPipeline = true)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContextWait, ValueFromPipeline = true)]
    public SessionContext? SessionContext { get; set; }

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
        var plan = CreatePlan();
        var client = SessionContext is null ? new ServiceBusClient(ServiceBusConnectionString) : null;

        if (SessionContext is not null)
        {
            var sessionCtxReceiver = SessionContext.Receiver;
            var isSessionReceiver = sessionCtxReceiver is ServiceBusSessionReceiver;
            ReceiveFromReceiver(sessionCtxReceiver, Peek, NoComplete, plan, cancellationToken, disposeReceiver: false, isSessionReceiver: isSessionReceiver);
            return;
        }

        try
        {
            if (IsQueueSet)
            {
                ServiceBusReceiver receiver;
                try
                {
                    receiver = client!.CreateReceiver(Queue);
                }
                catch (InvalidOperationException)
                {
                    ReceiveFromSessions(client!, Peek, NoComplete, plan, cancellationToken);
                    return;
                }

                try
                {
                    ReceiveFromReceiver(receiver, Peek, NoComplete, plan, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    ReceiveFromSessions(client!, Peek, NoComplete, plan, cancellationToken);
                }
                return;
            }

            ReceiveTopicPath(client!, plan, cancellationToken);
        }
        finally
        {
            client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void ReceiveTopicPath(ServiceBusClient client, ReceivePlan plan, CancellationToken cancellationToken)
    {
        try
        {
            var receiver = client.CreateReceiver(Topic, Subscription);
            ReceiveFromReceiver(receiver, Peek, NoComplete, plan, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            ReceiveFromSubscriptionSessions(client, Peek, NoComplete, plan, cancellationToken);
        }
    }

    private void ReceiveFromReceiver(ServiceBusReceiver receiver, bool peek, bool noComplete, ReceivePlan plan, CancellationToken cancellationToken, bool disposeReceiver = true, bool isSessionReceiver = false)
    {
        using var renewer = SessionLockAutoRenewer.Start(receiver, cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (plan.IsComplete || plan.DeadlineReached)
                {
                    return;
                }

                var window = plan.HasDeadline
                    ? plan.ComputeReceiveWindow(DefaultReceiveWindow)
                    : DefaultReceiveWindow;

                if (window <= TimeSpan.Zero)
                {
                    return;
                }

                IReadOnlyList<ServiceBusReceivedMessage> messages = peek
                    ? receiver.PeekMessagesAsync(BatchSize, cancellationToken: cancellationToken)
                        .GetAwaiter()
                        .GetResult()
                    : receiver.ReceiveMessagesAsync(BatchSize, window, cancellationToken)
                        .GetAwaiter()
                        .GetResult();

                if (messages.Count == 0)
                {
                    if (isSessionReceiver)
                    {
                        // release the current session to allow AcceptNextSessionAsync to pick another
                        break;
                    }

                    if (plan.DeadlineReached)
                    {
                        return;
                    }

                    Task.Delay(IdleDelay, cancellationToken).GetAwaiter().GetResult();
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

                    plan.OnMessage();
                    if (plan.IsComplete)
                    {
                        return;
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

    private void ReceiveFromSubscriptionSessions(ServiceBusClient client, bool peek, bool noComplete, ReceivePlan plan, CancellationToken cancellationToken)
    {
        ReceiveFromSessionAwareEntity(
            ct => client.AcceptNextSessionAsync(Topic, Subscription, cancellationToken: ct),
            peek,
            noComplete,
            plan,
            cancellationToken);
    }

    private void ReceiveFromSessions(ServiceBusClient client, bool peek, bool noComplete, ReceivePlan plan, CancellationToken cancellationToken)
    {
        ReceiveFromSessionAwareEntity(
            ct => client.AcceptNextSessionAsync(Queue, cancellationToken: ct),
            peek,
            noComplete,
            plan,
            cancellationToken);
    }

    private void ReceiveFromSessionAwareEntity(
        Func<CancellationToken, Task<ServiceBusSessionReceiver?>> acceptSessionAsync,
        bool peek,
        bool noComplete,
        ReceivePlan plan,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (plan.IsComplete || plan.DeadlineReached)
            {
                return;
            }

            var wait = plan.HasDeadline
                ? plan.ComputeReceiveWindow(DefaultReceiveWindow)
                : DefaultReceiveWindow;

            if (wait <= TimeSpan.Zero)
            {
                return;
            }

            ServiceBusSessionReceiver? sessionReceiver = null;
            using var acceptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            acceptCts.CancelAfter(wait);

            try
            {
                sessionReceiver = acceptSessionAsync(acceptCts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (acceptCts.IsCancellationRequested)
            {
                sessionReceiver = null;
            }

            if (sessionReceiver is null)
            {
                Task.Delay(IdleDelay, cancellationToken).GetAwaiter().GetResult();
                continue;
            }

            try
            {
                ReceiveFromReceiver(sessionReceiver, peek, noComplete, plan, cancellationToken, disposeReceiver: false, isSessionReceiver: true);
                if (plan.IsComplete)
                {
                    return;
                }
            }
            finally
            {
                sessionReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    private ReceivePlan CreatePlan()
    {
        int? max = IsMaxParameterSet ? MaxMessages : null;
        int? waitSeconds = IsWaitParameterSet ? WaitSeconds : null;
        return new ReceivePlan(max, waitSeconds);
    }

    private bool IsMaxParameterSet =>
        ParameterSetName is ParameterSetQueueMax or ParameterSetSubscriptionMax or ParameterSetContextMax;

    private bool IsWaitParameterSet =>
        ParameterSetName is ParameterSetQueueWait or ParameterSetSubscriptionWait or ParameterSetContextWait;

    private bool IsQueueSet =>
        ParameterSetName is ParameterSetQueue or ParameterSetQueueMax or ParameterSetQueueWait;

    private sealed class ReceivePlan
    {
        private readonly DateTime? _deadline;

        public ReceivePlan(int? maxMessages, int? waitSeconds)
        {
            Remaining = maxMessages;
            _deadline = waitSeconds.HasValue ? DateTime.UtcNow.AddSeconds(waitSeconds.Value) : null;
        }

        public int? Remaining { get; private set; }
        public bool HasDeadline => _deadline.HasValue;
        public bool DeadlineReached => _deadline.HasValue && DateTime.UtcNow >= _deadline.Value;
        public bool IsComplete => (Remaining.HasValue && Remaining.Value <= 0) || DeadlineReached;

        public TimeSpan ComputeReceiveWindow(TimeSpan defaultWindow)
        {
            if (!HasDeadline)
            {
                return defaultWindow;
            }

            var remaining = _deadline!.Value - DateTime.UtcNow;
            return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public void OnMessage()
        {
            if (Remaining.HasValue)
            {
                Remaining--;
            }
        }
    }
}
