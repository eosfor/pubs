using System;
using System.Management.Automation;
using System.Text.Json;
using System.Threading;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SBSessionState", DefaultParameterSetName = ParameterSetQueue)]
public sealed class GetSBSessionStateCommand : SBSessionAwareCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetContext = "Context";
    private const string ParameterSetSessionInfo = "SessionInfo";

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string SessionId { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(ParameterSetName = ParameterSetContext)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSessionInfo, ValueFromPipeline = true)]
    [Parameter(ParameterSetName = ParameterSetContext, ValueFromPipeline = true)]
    public SBSessionInfo? InputObject { get; set; }

    [Parameter]
    public SwitchParameter AsString { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContext, ValueFromPipeline = true)]
    public SessionContext? SessionContext { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var scope = CreateReceiverScope(cts.Token, out var receiver);
            using var renewer = SessionLockAutoRenewer.Start(receiver, cts.Token);
            WriteSessionState(receiver, cts.Token);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "GetSBSessionStateFailed", ErrorCategory.NotSpecified, ResolveErrorTarget()));
        }
    }

    private ReceiverScope CreateReceiverScope(CancellationToken ct, out ServiceBusSessionReceiver receiver)
    {
        if (ParameterSetName == ParameterSetContext)
        {
            if (SessionContext is null)
            {
                throw new ArgumentException("SessionContext is required for the Context parameter set.");
            }

            EnsureSessionContextTargetMatchesExplicit(SessionContext, Queue, Topic, Subscription);
            receiver = SessionContext.Receiver;
            return new ReceiverScope(null, null);
        }

        var connectionString = ResolveConnectionString();
        var client = CreateServiceBusClient(connectionString);
        receiver = CreateReceiver(client, connectionString, ct);
        return new ReceiverScope(client, receiver);
    }

    private ServiceBusSessionReceiver CreateReceiver(ServiceBusClient client, string connectionString, CancellationToken ct)
    {
        if (ParameterSetName == ParameterSetQueue)
        {
            var target = ResolveQueueOrSubscriptionTarget(
                Queue,
                Topic,
                Subscription,
                resolvedConnectionString: connectionString);
            if (target.Kind != ResolvedEntityKind.Queue)
            {
                throw new ArgumentException("Queue target is required for Queue parameter set.");
            }

            return client.AcceptSessionAsync(target.Queue, SessionId, cancellationToken: ct).GetAwaiter().GetResult();
        }

        if (ParameterSetName == ParameterSetSubscription)
        {
            var target = ResolveQueueOrSubscriptionTarget(
                Queue,
                Topic,
                Subscription,
                resolvedConnectionString: connectionString);
            if (target.Kind != ResolvedEntityKind.Subscription)
            {
                throw new ArgumentException("Topic and Subscription are required for Subscription parameter set.");
            }

            return client.AcceptSessionAsync(target.Topic, target.Subscription, SessionId, cancellationToken: ct).GetAwaiter().GetResult();
        }

        var sessionInfo = InputObject ?? throw new ArgumentException("Pipeline input session info is required.");
        if (string.IsNullOrWhiteSpace(sessionInfo.SessionId))
        {
            throw new ArgumentException("Pipeline input session info must contain SessionId.");
        }

        if (!string.IsNullOrWhiteSpace(sessionInfo.Queue))
        {
            return client.AcceptSessionAsync(sessionInfo.Queue, sessionInfo.SessionId, cancellationToken: ct).GetAwaiter().GetResult();
        }

        if (!string.IsNullOrWhiteSpace(sessionInfo.Topic) && !string.IsNullOrWhiteSpace(sessionInfo.Subscription))
        {
            return client.AcceptSessionAsync(sessionInfo.Topic, sessionInfo.Subscription, sessionInfo.SessionId, cancellationToken: ct).GetAwaiter().GetResult();
        }

        var entityPath = sessionInfo.EntityPath?.Trim('/') ?? string.Empty;
        if (TryParseTopicSubscription(entityPath, out var topic, out var subscription))
        {
            return client.AcceptSessionAsync(topic, subscription, sessionInfo.SessionId, cancellationToken: ct).GetAwaiter().GetResult();
        }

        if (!string.IsNullOrWhiteSpace(entityPath))
        {
            return client.AcceptSessionAsync(entityPath, sessionInfo.SessionId, cancellationToken: ct).GetAwaiter().GetResult();
        }

        throw new ArgumentException("Pipeline input session info must contain Queue or Topic/Subscription.");
    }

    private void WriteSessionState(ServiceBusSessionReceiver receiver, CancellationToken ct)
    {
        var state = receiver.GetSessionStateAsync(ct).GetAwaiter().GetResult();
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
        catch (JsonException)
        {
            WriteObject(state.ToString());
        }
    }

    private object ResolveErrorTarget()
    {
        if (ParameterSetName == ParameterSetContext)
        {
            return (object?)SessionContext?.EntityPath ?? this;
        }

        if (ParameterSetName == ParameterSetSessionInfo)
        {
            return (object?)InputObject ?? this;
        }

        return ParameterSetName == ParameterSetQueue
            ? Queue
            : $"{Topic}/{Subscription}";
    }

    private static bool TryParseTopicSubscription(string entityPath, out string topic, out string subscription)
    {
        const string subscriptionSegment = "/subscriptions/";
        topic = string.Empty;
        subscription = string.Empty;

        var markerIndex = entityPath.IndexOf(subscriptionSegment, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return false;
        }

        topic = entityPath[..markerIndex].Trim('/');
        subscription = entityPath[(markerIndex + subscriptionSegment.Length)..].Trim('/');
        return !string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(subscription);
    }

    private readonly struct ReceiverScope : IDisposable
    {
        private readonly ServiceBusClient? _client;
        private readonly ServiceBusSessionReceiver? _receiver;

        public ReceiverScope(ServiceBusClient? client, ServiceBusSessionReceiver? receiver)
        {
            _client = client;
            _receiver = receiver;
        }

        public void Dispose()
        {
            _receiver?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
