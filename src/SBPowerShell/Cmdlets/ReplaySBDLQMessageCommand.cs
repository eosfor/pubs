using System.Globalization;
using System.Management.Automation;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet("Replay", "SBDLQMessage", DefaultParameterSetName = ParameterSetQueue)]
[OutputType(typeof(ServiceBusReceivedMessage))]
public sealed class ReplaySBDLQMessageCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";

    private readonly List<ServiceBusReceivedMessage> _input = [];

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
    [ValidateNotNullOrEmpty]
    public string? DestinationQueue { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? DestinationTopic { get; set; }

    [Parameter]
    public SwitchParameter TransferDeadLetter { get; set; }

    [Parameter]
    public SwitchParameter NoCompleteSource { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxMessages { get; set; } = 100;

    [Parameter]
    [ValidateRange(1, 1000)]
    public int BatchSize { get; set; } = 50;

    [Parameter]
    [ValidateRange(1, 300)]
    public int WaitSeconds { get; set; } = 2;

    [Parameter(ValueFromPipeline = true)]
    public ServiceBusReceivedMessage[]? Message { get; set; }

    private string _resolvedConnectionString = string.Empty;
    private ResolvedEntity _resolvedTarget;

    protected override void ProcessRecord()
    {
        if (Message is { Length: > 0 })
        {
            _input.AddRange(Message);
        }
    }

    protected override void EndProcessing()
    {
        try
        {
            _resolvedConnectionString = ResolveConnectionString();
            _resolvedTarget = ResolveQueueOrSubscriptionTarget(
                Queue,
                Topic,
                Subscription,
                resolvedConnectionString: _resolvedConnectionString);

            var destination = ResolveDestination();
            var messages = _input.Count > 0 ? _input : ReadFromDlq();
            if (messages.Count == 0)
            {
                return;
            }

            ReplayToDestination(messages, destination);

            if (!NoCompleteSource)
            {
                CompleteSourceMessages(messages);
            }

            foreach (var message in messages)
            {
                WriteObject(message);
            }
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "ReplaySBDLQMessageFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private string ResolveDestination()
    {
        var hasQueue = !string.IsNullOrWhiteSpace(DestinationQueue);
        var hasTopic = !string.IsNullOrWhiteSpace(DestinationTopic);

        if (hasQueue == hasTopic)
        {
            throw new ArgumentException("Specify exactly one destination: -DestinationQueue or -DestinationTopic.");
        }

        return hasQueue ? DestinationQueue! : DestinationTopic!;
    }

    private List<ServiceBusReceivedMessage> ReadFromDlq()
    {
        var messages = new List<ServiceBusReceivedMessage>();
        var subQueue = ServiceBusSubQueuePath.ResolveSubQueue(TransferDeadLetter);

        var client = CreateServiceBusClient(_resolvedConnectionString);
        try
        {
            if (_resolvedTarget.Kind == ResolvedEntityKind.Queue)
            {
                ReadQueueDlq(client, _resolvedTarget.Queue, messages, subQueue);
            }
            else
            {
                ReadSubscriptionDlq(client, _resolvedTarget.Topic, _resolvedTarget.Subscription, messages, subQueue);
            }
        }
        finally
        {
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        return messages;
    }

    private void ReadQueueDlq(ServiceBusClient client, string queue, List<ServiceBusReceivedMessage> messages, SubQueue subQueue)
    {
        try
        {
            var receiver = client.CreateReceiver(queue, new ServiceBusReceiverOptions { SubQueue = subQueue });
            try
            {
                DrainReceiver(receiver, messages);
            }
            finally
            {
                receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        catch (InvalidOperationException)
        {
            var queuePath = ServiceBusSubQueuePath.BuildQueueEntityPath(queue);
            var sessionPath = ServiceBusSubQueuePath.BuildSessionPath(queuePath, subQueue);
            DrainSessionPath(client, sessionPath, messages);
        }
    }

    private void ReadSubscriptionDlq(ServiceBusClient client, string topic, string subscription, List<ServiceBusReceivedMessage> messages, SubQueue subQueue)
    {
        try
        {
            var receiver = client.CreateReceiver(topic, subscription, new ServiceBusReceiverOptions { SubQueue = subQueue });
            try
            {
                DrainReceiver(receiver, messages);
            }
            finally
            {
                receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        catch (InvalidOperationException)
        {
            var entityPath = ServiceBusSubQueuePath.BuildSubscriptionEntityPath(topic, subscription);
            var sessionPath = ServiceBusSubQueuePath.BuildSessionPath(entityPath, subQueue);
            DrainSessionPath(client, sessionPath, messages);
        }
    }

    private void DrainReceiver(ServiceBusReceiver receiver, List<ServiceBusReceivedMessage> messages)
    {
        while (messages.Count < MaxMessages)
        {
            var take = Math.Min(BatchSize, MaxMessages - messages.Count);
            var batch = receiver.ReceiveMessagesAsync(take, TimeSpan.FromSeconds(WaitSeconds)).GetAwaiter().GetResult();
            if (batch.Count == 0)
            {
                break;
            }

            messages.AddRange(batch);
        }
    }

    private void DrainSessionPath(ServiceBusClient client, string sessionPath, List<ServiceBusReceivedMessage> messages)
    {
        while (messages.Count < MaxMessages)
        {
            ServiceBusSessionReceiver? sessionReceiver = null;
            try
            {
                using var acceptCts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
                sessionReceiver = client.AcceptNextSessionAsync(sessionPath, cancellationToken: acceptCts.Token).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (sessionReceiver is null)
            {
                break;
            }

            try
            {
                DrainReceiver(sessionReceiver, messages);
            }
            finally
            {
                sessionReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    private void ReplayToDestination(IReadOnlyList<ServiceBusReceivedMessage> messages, string destination)
    {
        var client = CreateServiceBusClient(_resolvedConnectionString);
        try
        {
            var sender = client.CreateSender(destination);
            try
            {
                foreach (var source in messages)
                {
                    sender.SendMessageAsync(CloneForReplay(source)).GetAwaiter().GetResult();
                }
            }
            finally
            {
                sender.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        finally
        {
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void CompleteSourceMessages(IReadOnlyList<ServiceBusReceivedMessage> messages)
    {
        var subQueue = ServiceBusSubQueuePath.ResolveSubQueue(TransferDeadLetter);
        var client = CreateServiceBusClient(_resolvedConnectionString);
        try
        {
            var nonSession = messages.Where(m => string.IsNullOrEmpty(m.SessionId)).ToList();
            var sessionGroups = messages.Where(m => !string.IsNullOrEmpty(m.SessionId)).GroupBy(m => m.SessionId!).ToList();

            if (nonSession.Count > 0)
            {
                var receiver = _resolvedTarget.Kind == ResolvedEntityKind.Queue
                    ? client.CreateReceiver(_resolvedTarget.Queue, new ServiceBusReceiverOptions { SubQueue = subQueue })
                    : client.CreateReceiver(_resolvedTarget.Topic, _resolvedTarget.Subscription, new ServiceBusReceiverOptions { SubQueue = subQueue });

                try
                {
                    foreach (var message in nonSession)
                    {
                        receiver.CompleteMessageAsync(message).GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }

            foreach (var group in sessionGroups)
            {
                ServiceBusSessionReceiver sessionReceiver;
                if (_resolvedTarget.Kind == ResolvedEntityKind.Queue)
                {
                    var path = ServiceBusSubQueuePath.BuildSessionPath(ServiceBusSubQueuePath.BuildQueueEntityPath(_resolvedTarget.Queue), subQueue);
                    sessionReceiver = client.AcceptSessionAsync(path, group.Key, cancellationToken: default).GetAwaiter().GetResult();
                }
                else
                {
                    var path = ServiceBusSubQueuePath.BuildSessionPath(ServiceBusSubQueuePath.BuildSubscriptionEntityPath(_resolvedTarget.Topic, _resolvedTarget.Subscription), subQueue);
                    sessionReceiver = client.AcceptSessionAsync(path, group.Key, cancellationToken: default).GetAwaiter().GetResult();
                }

                try
                {
                    foreach (var message in group)
                    {
                        sessionReceiver.CompleteMessageAsync(message).GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    sessionReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
        }
        finally
        {
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static ServiceBusMessage CloneForReplay(ServiceBusReceivedMessage source)
    {
        var clone = new ServiceBusMessage(source.Body)
        {
            ContentType = source.ContentType,
            CorrelationId = source.CorrelationId,
            Subject = source.Subject,
            To = source.To,
            ReplyTo = source.ReplyTo,
            ReplyToSessionId = source.ReplyToSessionId,
            SessionId = source.SessionId,
            MessageId = source.MessageId
        };

        foreach (var property in source.ApplicationProperties)
        {
            clone.ApplicationProperties[property.Key] = property.Value;
        }

        if (source.TimeToLive > TimeSpan.Zero)
        {
            clone.TimeToLive = source.TimeToLive;
        }

        if (!string.IsNullOrWhiteSpace(source.PartitionKey))
        {
            clone.PartitionKey = source.PartitionKey;
        }

        if (!string.IsNullOrWhiteSpace(source.TransactionPartitionKey))
        {
            clone.TransactionPartitionKey = source.TransactionPartitionKey;
        }

        if (source.ScheduledEnqueueTime > DateTimeOffset.MinValue)
        {
            clone.ScheduledEnqueueTime = source.ScheduledEnqueueTime;
        }

        if (source.ApplicationProperties.TryGetValue("MessageId", out var idValue) && idValue is not null)
        {
            var id = Convert.ToString(idValue, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(id))
            {
                clone.MessageId = id;
            }
        }

        return clone;
    }
}
