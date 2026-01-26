using System.Management.Automation;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBSessionContext", DefaultParameterSetName = ParameterSetQueue)]
[OutputType(typeof(SessionContext))]
public sealed class NewSBSessionContextCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
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

    protected override void EndProcessing()
    {
        try
        {
            var client = new ServiceBusClient(ServiceBusConnectionString);

            ServiceBusSessionReceiver receiver = ParameterSetName == ParameterSetQueue
                ? client.AcceptSessionAsync(Queue, SessionId).GetAwaiter().GetResult()
                : client.AcceptSessionAsync(Topic, Subscription, SessionId).GetAwaiter().GetResult();

            var entityPath = ParameterSetName == ParameterSetQueue ? Queue : $"{Topic}/Subscriptions/{Subscription}";
            var ctx = new SessionContext(ServiceBusConnectionString, entityPath, SessionId, client, receiver);
            WriteObject(ctx);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "NewSBSessionContextFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
