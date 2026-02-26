using System;
using System.Management.Automation;
using System.Threading;
using SBPowerShell.Amqp;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBSession", DefaultParameterSetName = ParameterSetSubscription)]
[OutputType(typeof(SBSessionInfo))]
public sealed class GetSBSessionCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetQueue, Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSubscription, Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSubscription, Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter ActiveOnly { get; set; }

    [Parameter]
    public DateTime? LastUpdatedSince { get; set; }

    [Parameter]
    [ValidateRange(1, 600)]
    public int OperationTimeoutSec { get; set; } = 60;

    protected override void ProcessRecord()
    {
        try
        {
            if (ActiveOnly && LastUpdatedSince.HasValue)
            {
                throw new ArgumentException("Parameters ActiveOnly and LastUpdatedSince cannot be used together.");
            }

            var entityPath = ResolveEntityPath();
            var timeout = TimeSpan.FromSeconds(OperationTimeoutSec);
            using var cts = new CancellationTokenSource(timeout);

            // The heavy lifting is done by the low-level AMQP helper because Azure.Messaging.ServiceBus
            // does not expose a public "list sessions" API.
            var sessionIds = ServiceBusSessionEnumerator.GetSessionsAsync(
                ServiceBusConnectionString,
                entityPath,
                ActiveOnly.IsPresent,
                LastUpdatedSince,
                timeout,
                cts.Token).GetAwaiter().GetResult();

            foreach (var sessionId in sessionIds)
            {
                WriteObject(new SBSessionInfo
                {
                    SessionId = sessionId,
                    EntityPath = entityPath,
                    Queue = ParameterSetName == ParameterSetQueue ? Queue : null,
                    Topic = ParameterSetName == ParameterSetSubscription ? Topic : null,
                    Subscription = ParameterSetName == ParameterSetSubscription ? Subscription : null
                });
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "GetSBSessionFailed", ErrorCategory.NotSpecified, ResolveErrorTarget()));
        }
    }

    private string ResolveEntityPath()
    {
        return ParameterSetName == ParameterSetQueue
            ? Queue.Trim('/')
            : $"{Topic.Trim('/')}/Subscriptions/{Subscription.Trim('/')}";
    }

    private object ResolveErrorTarget()
    {
        return ParameterSetName == ParameterSetQueue
            ? Queue
            : $"{Topic}/{Subscription}";
    }
}
