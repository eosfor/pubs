using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Send, "SBScheduledMessage", DefaultParameterSetName = ParameterSetTopic)]
[OutputType(typeof(ScheduledMessageResult))]
public sealed class SendSBScheduledMessageCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetTopic = "Topic";

    private readonly List<PSMessage> _messages = new();

    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetTopic)]
    public PSMessage[]? Message { get; set; }

    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ValueFromPipeline = true, ParameterSetName = ParameterSetTopic)]
    public ServiceBusReceivedMessage[]? ReceivedInputObject { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetTopic)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public DateTimeOffset ScheduleAtUtc { get; set; }

    protected override void ProcessRecord()
    {
        if (Message is { Length: > 0 })
        {
            _messages.AddRange(Message);
        }

        if (ReceivedInputObject is { Length: > 0 })
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
            if (_messages.Count == 0)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("No messages provided. Specify -Message or pipe ServiceBusReceivedMessage objects."),
                    "SendSBScheduledMessageEmptyInput",
                    ErrorCategory.InvalidData,
                    this));
                return;
            }

            var connectionString = ResolveConnectionString();
            var target = ResolveQueueOrTopicTarget(Queue, Topic, resolvedConnectionString: connectionString);
            var entityPath = target.EntityPath;

            using var cts = new CancellationTokenSource();
            ScheduleInternal(connectionString, entityPath, cts.Token);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "SendSBScheduledMessageFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private void ScheduleInternal(string connectionString, string entityPath, CancellationToken cancellationToken)
    {
        var client = CreateServiceBusClient(connectionString);
        try
        {
            var sender = client.CreateSender(entityPath);
            try
            {
                foreach (var sbMessage in FlattenMessages(_messages))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sequenceNumber = sender.ScheduleMessageAsync(sbMessage, ScheduleAtUtc, cancellationToken)
                        .GetAwaiter()
                        .GetResult();

                    WriteObject(new ScheduledMessageResult
                    {
                        SequenceNumber = sequenceNumber,
                        EntityPath = entityPath,
                        SessionId = sbMessage.SessionId,
                        MessageId = sbMessage.MessageId,
                        ScheduledEnqueueTimeUtc = ScheduleAtUtc
                    });
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

    private IEnumerable<ServiceBusMessage> FlattenMessages(IEnumerable<PSMessage> messages)
    {
        foreach (var msg in messages)
        {
            var bodyList = msg.Body ?? [];
            var props = msg.CustomProperties ?? new Dictionary<string, object>();

            foreach (var body in bodyList)
            {
                yield return BuildServiceBusMessage(msg.SessionId, props, body);
            }
        }
    }

    private static ServiceBusMessage BuildServiceBusMessage(string? sessionId, IReadOnlyDictionary<string, object> customProperties, string body)
    {
        var sbMessage = new ServiceBusMessage(BinaryData.FromString(body));

        if (!string.IsNullOrEmpty(sessionId))
        {
            sbMessage.SessionId = sessionId;
        }

        foreach (var kvp in customProperties)
        {
            sbMessage.ApplicationProperties[kvp.Key] = kvp.Value;
        }

        if (customProperties.TryGetValue("MessageId", out var messageIdValue) && messageIdValue != null)
        {
            var messageIdString = Convert.ToString(messageIdValue, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(messageIdString))
            {
                sbMessage.MessageId = messageIdString;
            }
        }

        return sbMessage;
    }

    private static PSMessage ConvertFromReceived(ServiceBusReceivedMessage received)
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

        return new PSMessage(
            received.SessionId,
            props,
            [received.Body.ToString()]);
    }
}
