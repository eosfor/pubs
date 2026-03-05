using System.Management.Automation;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBSessionContext", DefaultParameterSetName = ParameterSetContextDefaults)]
[OutputType(typeof(SessionContext))]
public sealed class NewSBSessionContextCommand : SBEntityTargetCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetContextDefaults = "ContextDefaults";

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string SessionId { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetContextDefaults)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContextDefaults)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContextDefaults)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    protected override void EndProcessing()
    {
        try
        {
            var connectionString = ResolveConnectionString();
            var target = ResolveQueueOrSubscriptionTarget(Queue, Topic, Subscription, resolvedConnectionString: connectionString);
            var clientHandle = CreateServiceBusClientWithTransport(connectionString);
            var client = clientHandle.Client;

            ServiceBusSessionReceiver receiver = target.Kind == ResolvedEntityKind.Queue
                ? client.AcceptSessionAsync(target.Queue, SessionId).GetAwaiter().GetResult()
                : client.AcceptSessionAsync(target.Topic, target.Subscription, SessionId).GetAwaiter().GetResult();

            var entityPath = target.Kind == ResolvedEntityKind.Queue ? target.Queue : $"{target.Topic}/Subscriptions/{target.Subscription}";
            var ctx = target.Kind == ResolvedEntityKind.Queue
                ? new SessionContext(
                    connectionString,
                    entityPath,
                    SessionId,
                    client,
                    receiver,
                    clientHandle.TransportType,
                    queueName: target.Queue)
                : new SessionContext(
                    connectionString,
                    entityPath,
                    SessionId,
                    client,
                    receiver,
                    clientHandle.TransportType,
                    topicName: target.Topic,
                    subscriptionName: target.Subscription);
            WriteObject(ctx);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "NewSBSessionContextFailed", ErrorCategory.NotSpecified, this));
        }
    }
}
