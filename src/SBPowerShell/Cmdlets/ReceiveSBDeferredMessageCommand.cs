using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SBPowerShell.Models;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Receive, "SBDeferredMessage", DefaultParameterSetName = ParameterSetQueue)]
[OutputType(typeof(ServiceBusReceivedMessage))]
public sealed class ReceiveSBDeferredMessageCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetContext = "Context";

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public long[] SequenceNumber { get; set; } = Array.Empty<long>();

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
                ReceiveWithContext();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ServiceBusConnectionString))
                {
                    throw new ArgumentException("ServiceBusConnectionString is required when SessionContext is not provided.");
                }

                client = new ServiceBusClient(ServiceBusConnectionString);
                if (!string.IsNullOrEmpty(SessionId))
                {
                    ReceiveFromSession(client);
                }
                else
                {
                    ReceiveNonSession(client);
                }
            }
        }
        catch (Exception ex)
        {
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
        var messages = SessionContext!.Receiver.ReceiveDeferredMessagesAsync(SequenceNumber, _cts.Token)
            .GetAwaiter()
            .GetResult();

        foreach (var msg in messages)
        {
            WriteObject(msg);
        }
    }

    private void ReceiveNonSession(ServiceBusClient client)
    {
        var receiver = ParameterSetName == ParameterSetQueue
            ? client.CreateReceiver(Queue!)
            : client.CreateReceiver(Topic!, Subscription!);

        try
        {
            var messages = receiver.ReceiveDeferredMessagesAsync(SequenceNumber, _cts.Token)
                .GetAwaiter()
                .GetResult();

            foreach (var msg in messages)
            {
                WriteObject(msg);
            }
        }
        finally
        {
            receiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void ReceiveFromSession(ServiceBusClient client)
    {
        var sessionReceiver = ParameterSetName == ParameterSetQueue
            ? client.AcceptSessionAsync(Queue!, SessionId, cancellationToken: _cts.Token).GetAwaiter().GetResult()
            : client.AcceptSessionAsync(Topic!, Subscription!, SessionId, cancellationToken: _cts.Token).GetAwaiter().GetResult();

        try
        {
            var messages = sessionReceiver.ReceiveDeferredMessagesAsync(SequenceNumber, _cts.Token)
                .GetAwaiter()
                .GetResult();

            foreach (var msg in messages)
            {
                WriteObject(msg);
            }
        }
        finally
        {
            sessionReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private readonly CancellationTokenSource _cts = new();

    protected override void StopProcessing()
    {
        _cts.Cancel();
    }
}
