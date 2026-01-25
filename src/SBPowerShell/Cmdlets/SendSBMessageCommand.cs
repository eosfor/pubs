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

    private readonly List<PSMessage> _messages = new();
    private readonly CancellationTokenSource _cts = new();

    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetTopic)]
    public PSMessage[]? Message { get; set; }

    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetTopic)]
    public ServiceBusReceivedMessage[]? ReceivedInputObject { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetTopic)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter PerSessionThreadAuto { get; set; }

    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int PerSessionThread { get; set; }

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
                _messages.Add(ConvertFromReceived(received));
            }
        }
    }

    protected override void EndProcessing()
    {
        try
        {
            SendInternalAsync(_cts.Token).GetAwaiter().GetResult();
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

        if (PerSessionThreadAuto && PerSessionThread > 0)
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("Use only one parallelization switch: -PerSessionThreadAuto or -PerSessionThread."),
                "SendSBMessageParallelConflict",
                ErrorCategory.InvalidArgument,
                this));
            return;
        }

        var entity = ParameterSetName == ParameterSetQueue ? Queue : Topic;
        if (string.IsNullOrWhiteSpace(entity))
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("Queue or Topic name is required."),
                "SendSBMessageMissingTarget",
                ErrorCategory.InvalidArgument,
                this));
            return;
        }

        await using var client = new ServiceBusClient(ServiceBusConnectionString);

        if (PerSessionThreadAuto || PerSessionThread > 0)
        {
            EnsureAllHaveSessionId();
            await SendPerSessionAsync(client, entity, cancellationToken);
        }
        else
        {
            await SendSequentialAsync(client, entity, cancellationToken);
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

    private async Task SendSequentialAsync(ServiceBusClient client, string entity, CancellationToken cancellationToken)
    {
        await using var sender = client.CreateSender(entity);

        foreach (var sbMessage in FlattenMessages(_messages))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await sender.SendMessageAsync(sbMessage, cancellationToken);
        }
    }

    private async Task SendPerSessionAsync(ServiceBusClient client, string entity, CancellationToken cancellationToken)
    {
        var grouped = FlattenMessages(_messages)
            .GroupBy(m => m.SessionId)
            .ToList();

        var tasks = new List<Task>();

        foreach (var group in grouped)
        {
            if (PerSessionThreadAuto)
            {
                tasks.Add(SendSessionSequentialAsync(client, entity, group.ToList(), cancellationToken));
            }
            else
            {
                tasks.Add(SendSessionParallelAsync(client, entity, group.ToList(), PerSessionThread == 0 ? 1 : PerSessionThread, cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    private static async Task SendSessionSequentialAsync(ServiceBusClient client, string entity, IReadOnlyList<ServiceBusMessage> messages, CancellationToken cancellationToken)
    {
        await using var sender = client.CreateSender(entity);
        foreach (var msg in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await sender.SendMessageAsync(msg, cancellationToken);
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

    private IEnumerable<ServiceBusMessage> FlattenMessages(IEnumerable<PSMessage> messages)
    {
        foreach (var msg in messages)
        {
            var bodyList = msg.Body ?? Array.Empty<string>();
            var props = msg.CustomProperties ?? new Dictionary<string, object>();

            foreach (var body in bodyList)
            {
                yield return BuildServiceBusMessage(msg.SessionId, props, body);
            }
        }
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
