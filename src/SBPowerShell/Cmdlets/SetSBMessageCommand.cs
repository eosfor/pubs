using System.Collections.Generic;
using System.Management.Automation;
using SBPowerShell.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBMessage", DefaultParameterSetName = ParameterSetQueue)]
public sealed class SetSBMessageCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetSubscription = "Subscription";
    private const string ParameterSetContext = "Context";

    [Parameter(ParameterSetName = ParameterSetQueue)]
    [Parameter(ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetQueue)]
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetSubscription)]
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetContext)]
    public ServiceBusReceivedMessage[] Message { get; set; } = Array.Empty<ServiceBusReceivedMessage>();

    [Parameter]
    public SwitchParameter Complete { get; set; }

    [Parameter]
    public SwitchParameter Abandon { get; set; }

    [Parameter]
    public SwitchParameter Defer { get; set; }

    [Parameter]
    public SwitchParameter DeadLetter { get; set; }

    [Parameter]
    public string? DeadLetterReason { get; set; }

    [Parameter]
    public string? DeadLetterErrorDescription { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContext, ValueFromPipeline = true)]
    public SessionContext? SessionContext { get; set; }

    protected override void ProcessRecord()
    {
        if (Message is { Length: > 0 })
        {
            _messages.AddRange(Message);
        }
    }

    protected override void EndProcessing()
    {
        var actionCount = new[] { Complete, Abandon, Defer, DeadLetter }.Count(s => s.IsPresent);
        if (actionCount > 1)
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("Specify only one action: -Complete, -Abandon, -Defer, or -DeadLetter."),
                "SetSBMessageMultipleActions",
                ErrorCategory.InvalidArgument,
                this));
            return;
        }

        var action = actionCount == 0 ? SettlementAction.Complete : ResolveAction();

        if (_messages.Count == 0)
        {
            return;
        }

        try
        {
            var settled = SettleMessagesAsync(action, _cts.Token).GetAwaiter().GetResult();
            foreach (var m in settled)
            {
                WriteObject(m);
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SetSBMessageFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private readonly CancellationTokenSource _cts = new();
    private readonly List<ServiceBusReceivedMessage> _messages = new();

    protected override void StopProcessing()
    {
        _cts.Cancel();
    }

    private SettlementAction ResolveAction()
    {
        if (Abandon) return SettlementAction.Abandon;
        if (Defer) return SettlementAction.Defer;
        if (DeadLetter) return SettlementAction.DeadLetter;
        return SettlementAction.Complete;
    }

    private async Task<List<ServiceBusReceivedMessage>> SettleMessagesAsync(SettlementAction action, CancellationToken cancellationToken)
    {
        var settled = new List<ServiceBusReceivedMessage>();

        if (SessionContext is not null)
        {
            foreach (var msg in _messages)
            {
                await SettleAsync(SessionContext.Receiver, msg, action, cancellationToken);
                settled.Add(msg);
            }
            return settled;
        }

        if (string.IsNullOrWhiteSpace(ServiceBusConnectionString))
        {
            throw new ArgumentException("ServiceBusConnectionString is required when SessionContext is not provided.");
        }

        var client = new ServiceBusClient(ServiceBusConnectionString);
        try
        {
            var sessionGroups = _messages
                .Where(m => !string.IsNullOrEmpty(m.SessionId))
                .GroupBy(m => m.SessionId!)
                .ToList();

            var nonSessionMessages = _messages.Where(m => string.IsNullOrEmpty(m.SessionId)).ToList();

            if (nonSessionMessages.Count > 0)
            {
                await using var receiver = CreateReceiver(client);
                foreach (var msg in nonSessionMessages)
                {
                    await SettleAsync(receiver, msg, action, cancellationToken);
                    settled.Add(msg);
                }
            }

            foreach (var group in sessionGroups)
            {
                try
                {
                    await using var sessionReceiver = await CreateSessionReceiverAsync(client, group.Key, cancellationToken);
                    foreach (var msg in group)
                    {
                        await SettleAsync(sessionReceiver, msg, action, cancellationToken);
                        settled.Add(msg);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Entity is not session-enabled; fall back to non-session receiver even if messages contain SessionId.
                    await using var receiver = CreateReceiver(client);
                    foreach (var msg in group)
                    {
                        await SettleAsync(receiver, msg, action, cancellationToken);
                        settled.Add(msg);
                    }
                }
            }
        }
        finally
        {
            await client.DisposeAsync();
        }

        return settled;
    }

    private ServiceBusReceiver CreateReceiver(ServiceBusClient client)
    {
        return ParameterSetName == ParameterSetQueue
            ? client.CreateReceiver(Queue)
            : client.CreateReceiver(Topic, Subscription);
    }

    private async Task<ServiceBusSessionReceiver> CreateSessionReceiverAsync(ServiceBusClient client, string sessionId, CancellationToken ct)
    {
        return ParameterSetName == ParameterSetQueue
            ? await client.AcceptSessionAsync(Queue, sessionId, cancellationToken: ct)
            : await client.AcceptSessionAsync(Topic, Subscription, sessionId, cancellationToken: ct);
    }

    private async Task SettleAsync(ServiceBusReceiver receiver, ServiceBusReceivedMessage msg, SettlementAction action, CancellationToken ct)
    {
        switch (action)
        {
            case SettlementAction.Complete:
                await receiver.CompleteMessageAsync(msg, ct);
                break;
            case SettlementAction.Abandon:
                await receiver.AbandonMessageAsync(msg, cancellationToken: ct);
                break;
            case SettlementAction.Defer:
                await receiver.DeferMessageAsync(msg, cancellationToken: ct);
                break;
            case SettlementAction.DeadLetter:
                await receiver.DeadLetterMessageAsync(msg, DeadLetterReason, DeadLetterErrorDescription, ct);
                break;
        }
    }

    private enum SettlementAction
    {
        Complete,
        Abandon,
        Defer,
        DeadLetter
    }
}
