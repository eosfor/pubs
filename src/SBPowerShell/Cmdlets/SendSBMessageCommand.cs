using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Send, "SBMessage", DefaultParameterSetName = ParameterSetTopic)]
public sealed class SendSBMessageCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetTopic = "Topic";
    private const string ParameterSetContext = "Context";

    private readonly List<PSMessage> _messages = new();
    private readonly List<ServiceBusReceivedMessage> _receivedInputMessages = new();
    private readonly CancellationTokenSource _cts = new();

    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetTopic)]
    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetContext)]
    public PSMessage[]? Message { get; set; }

    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetTopic)]
    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetContext)]
    public ServiceBusReceivedMessage[]? ReceivedInputObject { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetTopic)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetTopic)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContext, ValueFromPipeline = true)]
    public SessionContext? SessionContext { get; set; }

    [Parameter]
    public SwitchParameter PerSessionThreadAuto { get; set; }

    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int PerSessionThread { get; set; }

    [Parameter]
    [ValidateRange(1, 1000)]
    public int BatchSize { get; set; } = 100;

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        if (Message is not null && Message.Length > 0)
        {
            _messages.AddRange(Message);
        }

        if (ReceivedInputObject is not null && ReceivedInputObject.Length > 0)
        {
            foreach (var received in ReceivedInputObject)
            {
                _receivedInputMessages.Add(received);
                _messages.Add(ConvertFromReceived(received));
            }
        }
    }

    protected override void EndProcessing()
    {
        try
        {
            SendInternalAsync(_cts.Token).GetAwaiter().GetResult();
            if (PassThru && _receivedInputMessages.Count > 0)
            {
                foreach (var received in _receivedInputMessages)
                {
                    WriteObject(received);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // cancellation requested by user; no extra logging
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SendSBMessageFailed", ErrorCategory.NotSpecified, this));
        }
    }

    protected override void StopProcessing()
    {
        _cts.Cancel();
    }

    private async Task SendInternalAsync(CancellationToken cancellationToken)
    {
        if (_messages.Count == 0)
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("No messages provided. Specify -Message or pipe ServiceBusReceivedMessage objects."),
                "SendSBMessageEmptyInput",
                ErrorCategory.InvalidData,
                this));
            return;
        }

        if (SessionContext is null && string.IsNullOrWhiteSpace(ServiceBusConnectionString))
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("ServiceBusConnectionString is required when SessionContext is not provided."),
                "SendSBMessageMissingConnectionString",
                ErrorCategory.InvalidArgument,
                this));
            return;
        }

        if (PerSessionThreadAuto && PerSessionThread > 0)
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("Use only one parallelization switch: -PerSessionThreadAuto or -PerSessionThread."),
                "SendSBMessageParallelConflict",
                ErrorCategory.InvalidArgument,
                this));
            return;
        }

        if (ParameterSetName == ParameterSetContext && (PerSessionThreadAuto || PerSessionThread > 0))
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("SessionContext mode does not support -PerSessionThreadAuto or -PerSessionThread."),
                "SendSBMessageContextParallelNotSupported",
                ErrorCategory.InvalidArgument,
                this));
            return;
        }

        var messages = FlattenMessages(_messages);
        if (messages.Count == 0)
        {
            return;
        }

        var entity = ResolveTargetEntity();
        if (string.IsNullOrWhiteSpace(entity))
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("Queue or Topic name is required."),
                "SendSBMessageMissingTarget",
                ErrorCategory.InvalidArgument,
                this));
            return;
        }

        ServiceBusClient? ownedClient = null;
        var client = SessionContext?.Client;
        if (client is null)
        {
            ownedClient = new ServiceBusClient(ServiceBusConnectionString);
            client = ownedClient;
        }

        try
        {
            if (PerSessionThreadAuto || PerSessionThread > 0)
            {
                EnsureAllHaveSessionId();
                await SendPerSessionAsync(client, entity, messages, cancellationToken);
            }
            else
            {
                await SendSequentialAsync(client, entity, messages, cancellationToken);
            }
        }
        finally
        {
            if (ownedClient is not null)
            {
                await ownedClient.DisposeAsync();
            }
        }
    }

    private void EnsureAllHaveSessionId()
    {
        var anyMissing = _messages.Any(m => string.IsNullOrEmpty(m.SessionId));
        if (anyMissing)
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("Parallel per-session modes require SessionId on every message."),
                "SendSBMessageSessionMissing",
                ErrorCategory.InvalidData,
                this));
        }
    }

    private string ResolveTargetEntity()
    {
        if (ParameterSetName == ParameterSetQueue)
        {
            return Queue;
        }

        if (ParameterSetName == ParameterSetTopic)
        {
            return Topic;
        }

        if (!string.IsNullOrWhiteSpace(Queue) && !string.IsNullOrWhiteSpace(Topic))
        {
            throw new ArgumentException("Specify only one explicit destination for SessionContext mode: -Queue or -Topic.");
        }

        if (!string.IsNullOrWhiteSpace(Queue))
        {
            return Queue;
        }

        if (!string.IsNullOrWhiteSpace(Topic))
        {
            return Topic;
        }

        if (SessionContext is null)
        {
            return string.Empty;
        }

        if (SessionContext.IsQueue && !string.IsNullOrWhiteSpace(SessionContext.QueueName))
        {
            return SessionContext.QueueName;
        }

        if (SessionContext.IsSubscription && !string.IsNullOrWhiteSpace(SessionContext.TopicName))
        {
            return SessionContext.TopicName;
        }

        var entityPath = SessionContext.EntityPath ?? string.Empty;
        var marker = "/Subscriptions/";
        var idx = entityPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return entityPath[..idx];
        }

        return entityPath;
    }

    private async Task SendSequentialAsync(ServiceBusClient client, string entity, IReadOnlyList<ServiceBusMessage> messages, CancellationToken cancellationToken)
    {
        await using var sender = client.CreateSender(entity);
        await SendBatchedAsync(sender, messages, cancellationToken);
    }

    private async Task SendPerSessionAsync(ServiceBusClient client, string entity, IReadOnlyList<ServiceBusMessage> messages, CancellationToken cancellationToken)
    {
        var grouped = messages
            .GroupBy(m => m.SessionId)
            .ToList();

        var tasks = new List<Task>();

        foreach (var group in grouped)
        {
            if (PerSessionThreadAuto)
            {
                tasks.Add(SendSessionSequentialAsync(client, entity, group.ToList(), BatchSize, cancellationToken));
            }
            else
            {
                tasks.Add(SendSessionParallelAsync(client, entity, group.ToList(), PerSessionThread == 0 ? 1 : PerSessionThread, cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    private static async Task SendSessionSequentialAsync(ServiceBusClient client, string entity, IReadOnlyList<ServiceBusMessage> messages, int batchSize, CancellationToken cancellationToken)
    {
        await using var sender = client.CreateSender(entity);
        ServiceBusMessageBatch? batch = null;
        try
        {
            batch = await sender.CreateMessageBatchAsync(cancellationToken);
            var inBatch = 0;

            foreach (var msg in messages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!batch.TryAddMessage(msg))
                {
                    if (inBatch == 0)
                    {
                        throw new InvalidOperationException("Message is too large to fit into a Service Bus batch.");
                    }

                    await sender.SendMessagesAsync(batch, cancellationToken);
                    batch.Dispose();
                    batch = await sender.CreateMessageBatchAsync(cancellationToken);
                    inBatch = 0;

                    if (!batch.TryAddMessage(msg))
                    {
                        throw new InvalidOperationException("Message is too large to fit into a Service Bus batch.");
                    }
                }

                inBatch++;
                if (inBatch >= batchSize)
                {
                    await sender.SendMessagesAsync(batch, cancellationToken);
                    batch.Dispose();
                    batch = await sender.CreateMessageBatchAsync(cancellationToken);
                    inBatch = 0;
                }
            }

            if (inBatch > 0)
            {
                await sender.SendMessagesAsync(batch, cancellationToken);
            }
        }
        finally
        {
            batch?.Dispose();
        }
    }

    private static async Task SendSessionParallelAsync(ServiceBusClient client, string entity, IReadOnlyList<ServiceBusMessage> messages, int degree, CancellationToken cancellationToken)
    {
        var queue = new ConcurrentQueue<ServiceBusMessage>(messages);
        var workers = new List<Task>();

        for (var i = 0; i < degree; i++)
        {
            workers.Add(Task.Run(async () =>
            {
                await using var sender = client.CreateSender(entity);
                while (!cancellationToken.IsCancellationRequested && queue.TryDequeue(out var msg))
                {
                    await sender.SendMessageAsync(msg, cancellationToken);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(workers);
    }

    private async Task SendBatchedAsync(ServiceBusSender sender, IReadOnlyList<ServiceBusMessage> messages, CancellationToken cancellationToken)
    {
        ServiceBusMessageBatch? batch = null;
        try
        {
            batch = await sender.CreateMessageBatchAsync(cancellationToken);
            var inBatch = 0;
            foreach (var msg in messages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!batch.TryAddMessage(msg))
                {
                    if (inBatch == 0)
                    {
                        throw new InvalidOperationException("Message is too large to fit into a Service Bus batch.");
                    }

                    await sender.SendMessagesAsync(batch, cancellationToken);
                    batch.Dispose();
                    batch = await sender.CreateMessageBatchAsync(cancellationToken);
                    inBatch = 0;

                    if (!batch.TryAddMessage(msg))
                    {
                        throw new InvalidOperationException("Message is too large to fit into a Service Bus batch.");
                    }
                }

                inBatch++;
                if (inBatch >= BatchSize)
                {
                    await sender.SendMessagesAsync(batch, cancellationToken);
                    batch.Dispose();
                    batch = await sender.CreateMessageBatchAsync(cancellationToken);
                    inBatch = 0;
                }
            }

            if (inBatch > 0)
            {
                await sender.SendMessagesAsync(batch, cancellationToken);
            }
        }
        finally
        {
            batch?.Dispose();
        }
    }

    private List<ServiceBusMessage> FlattenMessages(IEnumerable<PSMessage> messages)
    {
        var flattened = new List<ServiceBusMessage>();
        foreach (var msg in messages)
        {
            var bodyList = msg.Body ?? Array.Empty<string>();
            var props = msg.CustomProperties ?? new Dictionary<string, object>();

            foreach (var body in bodyList)
            {
                flattened.Add(BuildServiceBusMessage(msg.SessionId, props, body));
            }
        }

        return flattened;
    }

    private ServiceBusMessage BuildServiceBusMessage(string? sessionId, IReadOnlyDictionary<string, object> customProperties, string body)
    {
        var sbMessage = new ServiceBusMessage(BinaryData.FromString(body));

        if (!string.IsNullOrEmpty(sessionId))
        {
            sbMessage.SessionId = sessionId;
        }

        foreach (var kvp in customProperties)
        {
            sbMessage.ApplicationProperties[kvp.Key] = kvp.Value ?? DBNull.Value;
        }

        if (customProperties.TryGetValue("MessageId", out var messageIdValue) && messageIdValue != null)
        {
            var messageIdString = Convert.ToString(messageIdValue, CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(messageIdString))
            {
                throw new ArgumentException("Custom property 'MessageId' must convert to a non-empty string.");
            }

            sbMessage.MessageId = messageIdString;
        }

        return sbMessage;
    }

    private PSMessage ConvertFromReceived(ServiceBusReceivedMessage received)
    {
        var props = new Dictionary<string, object>();
        foreach (var kv in received.ApplicationProperties)
        {
            props[kv.Key] = kv.Value;
        }

        if (!string.IsNullOrEmpty(received.MessageId))
        {
            props["MessageId"] = received.MessageId;
        }

        var bodyText = received.Body.ToString();

        return new PSMessage(
            received.SessionId,
            props,
            new[] { bodyText });
    }
}
