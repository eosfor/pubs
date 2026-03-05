using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Internal;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Receive, "SBDeferredMessage", DefaultParameterSetName = ParameterSetQueue)]
[OutputType(typeof(ServiceBusReceivedMessage))]
public sealed class ReceiveSBDeferredMessageCommand : SBSessionAwareCmdletBase
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetContext = "Context";

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public long[] SequenceNumber { get; set; } = Array.Empty<long>();

    [Parameter]
    [ValidateRange(1, 1000)]
    public int ChunkSize { get; set; } = 200;

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string? Queue { get; set; }

    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string? Topic { get; set; }

    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string? Subscription { get; set; }

    [Parameter]
    public string? SessionId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContext, ValueFromPipeline = true)]
    public SessionContext? SessionContext { get; set; }

    protected override void EndProcessing()
    {
        if (SequenceNumber.Length == 0)
        {
            return;
        }

        ServiceBusClient? client = null;
        try
        {
            if (SessionContext is not null)
            {
                EnsureSessionContextTargetMatchesExplicit(SessionContext, Queue, Topic, Subscription);
                ReceiveWithContext();
            }
            else
            {
                var connectionString = ResolveConnectionString();
                var target = ResolveQueueOrSubscriptionTarget(
                    Queue,
                    Topic,
                    Subscription,
                    resolvedConnectionString: connectionString);
                client = CreateServiceBusClient(connectionString);
                if (!string.IsNullOrEmpty(SessionId))
                {
                    ReceiveFromSession(client, target);
                }
                else
                {
                    ReceiveNonSession(client, target);
                }
            }
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "ReceiveSBDeferredMessageFailed", ErrorCategory.NotSpecified, this));
        }
        finally
        {
            if (client is not null)
            {
                client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    private void ReceiveWithContext()
    {
        using var renewer = SessionLockAutoRenewer.Start(SessionContext!.Receiver, _cts.Token);
        foreach (var chunk in ChunkSequenceNumbers())
        {
            var messages = SessionContext!.Receiver.ReceiveDeferredMessagesAsync(chunk, _cts.Token)
                .GetAwaiter()
                .GetResult();

            foreach (var msg in messages)
            {
                WriteObject(msg);
            }
        }
    }

    private void ReceiveNonSession(ServiceBusClient client, ResolvedEntity target)
    {
        var receiver = target.Kind == ResolvedEntityKind.Queue
            ? client.CreateReceiver(target.Queue)
            : client.CreateReceiver(target.Topic, target.Subscription);

        try
        {
            foreach (var chunk in ChunkSequenceNumbers())
            {
                var messages = receiver.ReceiveDeferredMessagesAsync(chunk, _cts.Token)
                    .GetAwaiter()
                    .GetResult();

                foreach (var msg in messages)
                {
                    WriteObject(msg);
                }
            }
        }
        finally
        {
            receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void ReceiveFromSession(ServiceBusClient client, ResolvedEntity target)
    {
        var sessionReceiver = target.Kind == ResolvedEntityKind.Queue
            ? client.AcceptSessionAsync(target.Queue, SessionId, cancellationToken: _cts.Token).GetAwaiter().GetResult()
            : client.AcceptSessionAsync(target.Topic, target.Subscription, SessionId, cancellationToken: _cts.Token).GetAwaiter().GetResult();

        try
        {
            using var renewer = SessionLockAutoRenewer.Start(sessionReceiver, _cts.Token);
            foreach (var chunk in ChunkSequenceNumbers())
            {
                var messages = sessionReceiver.ReceiveDeferredMessagesAsync(chunk, _cts.Token)
                    .GetAwaiter()
                    .GetResult();

                foreach (var msg in messages)
                {
                    WriteObject(msg);
                }
            }
        }
        finally
        {
            sessionReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private IEnumerable<long[]> ChunkSequenceNumbers()
    {
        for (var i = 0; i < SequenceNumber.Length; i += ChunkSize)
        {
            var size = Math.Min(ChunkSize, SequenceNumber.Length - i);
            var chunk = new long[size];
            Array.Copy(SequenceNumber, i, chunk, 0, size);
            yield return chunk;
        }
    }

    private readonly CancellationTokenSource _cts = new();

    protected override void StopProcessing()
    {
        _cts.Cancel();
    }
}
