using System.Management.Automation;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBScheduledMessage", DefaultParameterSetName = ParameterSetQueue, SupportsShouldProcess = true)]
public sealed class RemoveSBScheduledMessageCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetTopic = "Topic";

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetTopic)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public long[] SequenceNumber { get; set; } = [];

    private readonly List<long> _numbers = [];

    protected override void ProcessRecord()
    {
        if (SequenceNumber is { Length: > 0 })
        {
            _numbers.AddRange(SequenceNumber);
        }
    }

    protected override void EndProcessing()
    {
        if (_numbers.Count == 0)
        {
            return;
        }

        var connectionString = ResolveConnectionString();
        var target = ResolveQueueOrTopicTarget(Queue, Topic, resolvedConnectionString: connectionString);
        var entityPath = target.EntityPath;
        if (!ShouldProcess($"{(target.Kind == ResolvedEntityKind.Queue ? "Queue" : "Topic")} '{entityPath}' (from {target.Source})", $"Cancel {_numbers.Count} scheduled message(s)"))
        {
            return;
        }

        try
        {
            var client = CreateServiceBusClient(connectionString);
            try
            {
                var sender = client.CreateSender(entityPath);
                try
                {
                    sender.CancelScheduledMessagesAsync(_numbers).GetAwaiter().GetResult();
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
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBScheduledMessageFailed", ErrorCategory.NotSpecified, entityPath));
        }
    }
}
