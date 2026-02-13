using System.Management.Automation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBSessionState", DefaultParameterSetName = ParameterSetQueue)]
public sealed class GetSBSessionStateCommand : PSCmdlet
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

    [Parameter]
    public SwitchParameter AsString { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContext, ValueFromPipeline = true)]
    public SessionContext? SessionContext { get; set; }

    protected override void EndProcessing()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            ServiceBusSessionReceiver receiver;
            ServiceBusClient? client = null;
            if (SessionContext is not null)
            {
                receiver = SessionContext.Receiver;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ServiceBusConnectionString))
                {
                    throw new ArgumentException("ServiceBusConnectionString is required when SessionContext is not provided.");
                }
                client = new ServiceBusClient(ServiceBusConnectionString);
                receiver = ParameterSetName == ParameterSetQueue
                    ? client.AcceptSessionAsync(Queue, SessionId, cancellationToken: cts.Token).GetAwaiter().GetResult()
                    : client.AcceptSessionAsync(Topic, Subscription, SessionId, cancellationToken: cts.Token).GetAwaiter().GetResult();
            }

            try
            {
                using var renewer = SessionLockAutoRenewer.Start(receiver, cts.Token);
                var state = receiver.GetSessionStateAsync(cts.Token).GetAwaiter().GetResult();
                if (state is null)
                {
                    WriteObject(null);
                    return;
                }

                if (AsString)
                {
                    WriteObject(state.ToString());
                    return;
                }

                var model = SessionOrderingStateSerializer.Deserialize(state);
                if (model is not null)
                {
                    WriteObject(model);
                    return;
                }

                try
                {
                    var doc = JsonDocument.Parse(state.ToStream());
                    WriteObject(doc.RootElement.Clone());
                }
                catch
                {
                    WriteObject(state.ToString());
                }
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
            ThrowTerminatingError(new ErrorRecord(ex, "GetSBSessionStateFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
