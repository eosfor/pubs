using System.Management.Automation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBSessionState", DefaultParameterSetName = ParameterSetQueue)]
public sealed class SetSBSessionStateCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetContext = "Context";

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string SessionId { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public object State { get; set; } = null!;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContext, ValueFromPipeline = true)]
    public SessionContext? SessionContext { get; set; }

    protected override void EndProcessing()
    {
        try
        {
            if (SessionContext is null && string.IsNullOrWhiteSpace(ServiceBusConnectionString))
            {
                throw new ArgumentException("ServiceBusConnectionString is required when SessionContext is not provided.");
            }

            var client = SessionContext is null ? new ServiceBusClient(ServiceBusConnectionString) : null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            ServiceBusSessionReceiver receiver = SessionContext?.Receiver ?? (ParameterSetName == ParameterSetQueue
                ? client!.AcceptSessionAsync(Queue, SessionId, cancellationToken: cts.Token).GetAwaiter().GetResult()
                : client!.AcceptSessionAsync(Topic, Subscription, SessionId, cancellationToken: cts.Token).GetAwaiter().GetResult());

            try
            {
                var binary = ToBinaryData(State);
                receiver.SetSessionStateAsync(binary, cts.Token).GetAwaiter().GetResult();
            }
            finally
            {
                if (SessionContext is null)
                {
                    receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SetSBSessionStateFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private static BinaryData ToBinaryData(object value)
    {
        if (value is null)
        {
            return BinaryData.FromString(string.Empty);
        }

        if (value is PSObject psObj)
        {
            value = psObj.BaseObject ?? string.Empty;
        }

        if (value is string s)
        {
            return BinaryData.FromString(s);
        }

        if (value is byte[] bytes)
        {
            return new BinaryData(bytes);
        }

        return BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(value));
    }
}
