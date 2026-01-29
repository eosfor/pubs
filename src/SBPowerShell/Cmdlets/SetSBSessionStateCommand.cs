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
            EnsureConnectionString();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var scope = CreateReceiver(cts.Token, out var receiver);

            var binary = ToBinaryData(State);
            receiver.SetSessionStateAsync(binary, cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SetSBSessionStateFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private void EnsureConnectionString()
    {
        if (SessionContext is null && string.IsNullOrWhiteSpace(ServiceBusConnectionString))
        {
            throw new ArgumentException("ServiceBusConnectionString is required when SessionContext is not provided.");
        }
    }

    private ReceiverScope CreateReceiver(CancellationToken ct, out ServiceBusSessionReceiver receiver)
    {
        if (SessionContext is not null)
        {
            receiver = SessionContext.Receiver;
            return new ReceiverScope(null, null);
        }

        var client = new ServiceBusClient(ServiceBusConnectionString);
        receiver = ParameterSetName == ParameterSetQueue
            ? client.AcceptSessionAsync(Queue, SessionId, cancellationToken: ct).GetAwaiter().GetResult()
            : client.AcceptSessionAsync(Topic, Subscription, SessionId, cancellationToken: ct).GetAwaiter().GetResult();

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
