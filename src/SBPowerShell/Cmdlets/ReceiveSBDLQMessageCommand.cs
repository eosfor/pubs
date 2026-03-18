using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Receive, "SBDLQMessage", DefaultParameterSetName = ParameterSetQueue)]
[OutputType(typeof(ServiceBusReceivedMessage))]
public sealed class ReceiveSBDLQMessageCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetQueueMax = "QueueMax";
    private const string ParameterSetQueueWait = "QueueWait";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetSubscriptionMax = "SubscriptionMax";
    private const string ParameterSetSubscriptionWait = "SubscriptionWait";

    private static readonly TimeSpan DefaultReceiveWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);

    private readonly CancellationTokenSource _cts = new();

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetQueueMax)]
    [Parameter(ParameterSetName = ParameterSetQueueWait)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueueMax)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscriptionMax)]
    [ValidateRange(1, int.MaxValue)]
    public int MaxMessages { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetQueueMax)]
    [Parameter(ParameterSetName = ParameterSetQueueWait)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
    [ValidateRange(1, 1000)]
    public int BatchSize { get; set; } = 10;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueueWait)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscriptionWait)]
    [ValidateRange(1, 300)]
    public int WaitSeconds { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetQueueMax)]
    [Parameter(ParameterSetName = ParameterSetQueueWait)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
    public SwitchParameter Peek { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetQueueMax)]
    [Parameter(ParameterSetName = ParameterSetQueueWait)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionMax)]
    [Parameter(ParameterSetName = ParameterSetSubscriptionWait)]
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
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "ReceiveSBDLQMessageFailed", ErrorCategory.NotSpecified, this));
        }
    }

    protected override void StopProcessing()
    {
        _cts.Cancel();
    }

    private void Receive(CancellationToken cancellationToken)
    {
        var plan = CreatePlan();
        var connectionString = ResolveConnectionString();
        var target = ResolveQueueOrSubscriptionTarget(
            Queue,
            Topic,
            Subscription,
            resolvedConnectionString: connectionString);
        ServiceBusClient? client = null;
        try
        {
            client = CreateServiceBusClient(connectionString, CreateClientOptions(plan));

            if (target.Kind == ResolvedEntityKind.Queue)
            {
                ReceiveQueue(client, target.Queue, plan, cancellationToken);
            }
            else
            {
                ReceiveSubscription(client, target.Topic, target.Subscription, plan, cancellationToken);
            }
        }
        finally
        {
            client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void ReceiveQueue(ServiceBusClient client, string queue, ReceivePlan plan, CancellationToken cancellationToken)
    {
        try
        {
            var receiver = client.CreateReceiver(
                queue,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            ReceiveFromReceiver(receiver, Peek, NoComplete, plan, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            ReceiveFromDeadLetterSessions(client, queue, plan, cancellationToken);
        }
    }

    private void ReceiveSubscription(ServiceBusClient client, string topic, string subscription, ReceivePlan plan, CancellationToken cancellationToken)
    {
        try
        {
            var receiver = client.CreateReceiver(
                topic,
                subscription,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            ReceiveFromReceiver(receiver, Peek, NoComplete, plan, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            var entityPath = $"{topic}/Subscriptions/{subscription}";
            ReceiveFromDeadLetterSessions(client, entityPath, plan, cancellationToken);
        }
    }

    private void ReceiveFromReceiver(ServiceBusReceiver receiver, bool peek, bool noComplete, ReceivePlan plan, CancellationToken cancellationToken, bool disposeReceiver = true, bool isSessionReceiver = false)
    {
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

                IReadOnlyList<ServiceBusReceivedMessage> messages = ReceiveBatch(receiver, peek, window, cancellationToken, plan.HasDeadline);

                if (messages.Count == 0)
                {
                    if (isSessionReceiver)
                    {
                        // release current session so another can be accepted
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
                DisposeReceiver(receiver, suppressTimeoutErrors: plan.HasDeadline);
            }
        }
    }

    private IReadOnlyList<ServiceBusReceivedMessage> ReceiveBatch(
        ServiceBusReceiver receiver,
        bool peek,
        TimeSpan window,
        CancellationToken cancellationToken,
        bool hasDeadline)
    {
        using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        receiveCts.CancelAfter(window);

        try
        {
            return peek
                ? receiver.PeekMessagesAsync(BatchSize, cancellationToken: receiveCts.Token)
                    .GetAwaiter()
                    .GetResult()
                : receiver.ReceiveMessagesAsync(BatchSize, window, receiveCts.Token)
                    .GetAwaiter()
                    .GetResult();
        }
        catch (OperationCanceledException) when (receiveCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Array.Empty<ServiceBusReceivedMessage>();
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout && hasDeadline)
        {
            return Array.Empty<ServiceBusReceivedMessage>();
        }
    }

    private void ReceiveFromDeadLetterSessions(ServiceBusClient client, string entityPath, ReceivePlan plan, CancellationToken cancellationToken)
    {
        var deadLetterPath = $"{entityPath}/$DeadLetterQueue";

        ReceiveFromSessionAwareEntity(
            ct => client.AcceptNextSessionAsync(deadLetterPath, cancellationToken: ct),
            Peek,
            NoComplete,
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
                DisposeReceiver(sessionReceiver, suppressTimeoutErrors: plan.HasDeadline);
            }
        }
    }

    private static void DisposeReceiver(ServiceBusReceiver receiver, bool suppressTimeoutErrors)
    {
        try
        {
            receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (suppressTimeoutErrors)
        {
        }
        catch (TimeoutException) when (suppressTimeoutErrors)
        {
        }
        catch (ServiceBusException ex) when (suppressTimeoutErrors && ex.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
        }
    }

    private ReceivePlan CreatePlan()
    {
        int? max = IsMaxParameterSet ? MaxMessages : null;
        int? waitSeconds = IsWaitParameterSet ? WaitSeconds : null;
        return new ReceivePlan(max, waitSeconds);
    }

    private ServiceBusClientOptions CreateClientOptions(ReceivePlan plan)
    {
        var options = new ServiceBusClientOptions();
        if (!plan.HasDeadline)
        {
            return options;
        }

        var boundedTimeout = TimeSpan.FromSeconds(Math.Max(1, Math.Min(WaitSeconds, 30)));
        options.RetryOptions.TryTimeout = boundedTimeout;
        options.RetryOptions.MaxRetries = 0;
        return options;
    }

    private bool IsMaxParameterSet =>
        ParameterSetName is ParameterSetQueueMax or ParameterSetSubscriptionMax;

    private bool IsWaitParameterSet =>
        ParameterSetName is ParameterSetQueueWait or ParameterSetSubscriptionWait;

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
