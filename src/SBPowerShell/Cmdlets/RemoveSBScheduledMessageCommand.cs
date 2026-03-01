using System.Management.Automation;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SBScheduledMessage", DefaultParameterSetName = ParameterSetQueue, SupportsShouldProcess = true)]
public sealed class RemoveSBScheduledMessageCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetTopic = "Topic";

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetTopic)]
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

        var entityPath = ParameterSetName == ParameterSetQueue ? Queue : Topic;
        if (!ShouldProcess(entityPath, $"Cancel {_numbers.Count} scheduled message(s)"))
        {
            return;
        }

        try
        {
            var client = new ServiceBusClient(ServiceBusConnectionString);
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
            ThrowTerminatingError(new ErrorRecord(ex, "RemoveSBScheduledMessageFailed", ErrorCategory.NotSpecified, entityPath));
        }
    }
}
