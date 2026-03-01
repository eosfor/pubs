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
    private const string ParameterSetContext = "Context";

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContext)]
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

    [Parameter(ParameterSetName = ParameterSetContext, Mandatory = true, ValueFromPipeline = true)]
    public SessionContext? SessionContext { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    public SwitchParameter ActiveOnly { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    public DateTime? LastUpdatedSince { get; set; }

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateRange(1, 600)]
    public int OperationTimeoutSec { get; set; } = 60;

    protected override void ProcessRecord()
    {
        try
        {
            if (ParameterSetName == ParameterSetContext)
            {
                if (SessionContext is null)
                {
                    throw new ArgumentException("SessionContext is required for the Context parameter set.");
                }

                WriteObject(BuildSessionInfoFromContext(SessionContext));
                return;
            }

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
        if (ParameterSetName == ParameterSetContext)
        {
            return (object?)SessionContext?.EntityPath ?? this;
        }

        return ParameterSetName == ParameterSetQueue
            ? Queue
            : $"{Topic}/{Subscription}";
    }

    private static SBSessionInfo BuildSessionInfoFromContext(SessionContext context)
    {
        var entityPath = context.EntityPath.Trim('/');
        if (TryParseTopicSubscription(entityPath, out var topic, out var subscription))
        {
            return new SBSessionInfo
            {
                SessionId = context.SessionId,
                EntityPath = entityPath,
                Topic = topic,
                Subscription = subscription
            };
        }

        return new SBSessionInfo
        {
            SessionId = context.SessionId,
            EntityPath = entityPath,
            Queue = entityPath
        };
    }

    private static bool TryParseTopicSubscription(string entityPath, out string topic, out string subscription)
    {
        const string subscriptionSegment = "/subscriptions/";
        topic = string.Empty;
        subscription = string.Empty;

        var normalized = entityPath.Trim('/');
        var markerIndex = normalized.IndexOf(subscriptionSegment, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return false;
        }

        topic = normalized[..markerIndex].Trim('/');
        subscription = normalized[(markerIndex + subscriptionSegment.Length)..].Trim('/');
        return !string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription);
    }
}
