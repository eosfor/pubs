using System.Management.Automation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBSessionState", DefaultParameterSetName = ParameterSetQueue)]
public sealed class SetSBSessionStateCommand : SBSessionAwareCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetContext = "Context";

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string SessionId { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetSubscription)]
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var scope = CreateReceiver(cts.Token, out var receiver);
            using var renewer = SessionLockAutoRenewer.Start(receiver, cts.Token);

            var binary = ToBinaryData(State);
            receiver.SetSessionStateAsync(binary, cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "SetSBSessionStateFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private ReceiverScope CreateReceiver(CancellationToken ct, out ServiceBusSessionReceiver receiver)
    {
        if (SessionContext is not null)
        {
            EnsureSessionContextTargetMatchesExplicit(SessionContext, Queue, Topic, Subscription);
            receiver = SessionContext.Receiver;
            return new ReceiverScope(null, null);
        }

        var connectionString = ResolveConnectionString();
        var target = ResolveQueueOrSubscriptionTarget(
            Queue,
            Topic,
            Subscription,
            resolvedConnectionString: connectionString);
        var client = CreateServiceBusClient(connectionString);
        receiver = target.Kind == ResolvedEntityKind.Queue
            ? client.AcceptSessionAsync(target.Queue, SessionId, cancellationToken: ct).GetAwaiter().GetResult()
            : client.AcceptSessionAsync(target.Topic, target.Subscription, SessionId, cancellationToken: ct).GetAwaiter().GetResult();

        return new ReceiverScope(client, receiver);
    }

    private static BinaryData ToBinaryData(object? value)
    {
        value = Unwrap(value);

        if (value is null)
        {
            return BinaryData.FromString(string.Empty);
        }

        if (value is SessionOrderingState typedState)
        {
            return SessionOrderingStateSerializer.Serialize(typedState);
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

    private static object? Unwrap(object? value)
    {
        if (value is PSObject psObj)
        {
            return psObj.BaseObject;
        }

        return value;
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
