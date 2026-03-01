using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBEntityStatus", DefaultParameterSetName = ParameterSetQueue, SupportsShouldProcess = true)]
public sealed class SetSBEntityStatusCommand : PSCmdlet
{
    private const string ParameterSetQueue = "Queue";
    private const string ParameterSetTopic = "Topic";
    private const string ParameterSetSubscription = "Subscription";

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetQueue)]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetTopic)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSubscription)]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateSet("Active", "Disabled", "SendDisabled", "ReceiveDisabled")]
    public string Status { get; set; } = "Active";

    protected override void ProcessRecord()
    {
        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var status = ResolveStatus(Status);

            switch (ParameterSetName)
            {
                case ParameterSetQueue:
                    SetQueueStatus(admin, status);
                    break;
                case ParameterSetTopic:
                    SetTopicStatus(admin, status);
                    break;
                case ParameterSetSubscription:
                    SetSubscriptionStatus(admin, status);
                    break;
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SetSBEntityStatusFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private void SetQueueStatus(ServiceBusAdministrationClient admin, EntityStatus status)
    {
        if (!ShouldProcess(Queue, $"Set status to {Status}"))
        {
            return;
        }

        var queue = admin.GetQueueAsync(Queue).GetAwaiter().GetResult().Value;
        queue.Status = status;
        var updated = admin.UpdateQueueAsync(queue).GetAwaiter().GetResult().Value;
        WriteObject(updated);
    }

    private void SetTopicStatus(ServiceBusAdministrationClient admin, EntityStatus status)
    {
        if (!ShouldProcess(Topic, $"Set status to {Status}"))
        {
            return;
        }

        var topic = admin.GetTopicAsync(Topic).GetAwaiter().GetResult().Value;
        topic.Status = status;
        var updated = admin.UpdateTopicAsync(topic).GetAwaiter().GetResult().Value;
        WriteObject(updated);
    }

    private void SetSubscriptionStatus(ServiceBusAdministrationClient admin, EntityStatus status)
    {
        var entityPath = $"{Topic}/Subscriptions/{Subscription}";
        if (!ShouldProcess(entityPath, $"Set status to {Status}"))
        {
            return;
        }

        var subscription = admin.GetSubscriptionAsync(Topic, Subscription).GetAwaiter().GetResult().Value;
        subscription.Status = status;
        var updated = admin.UpdateSubscriptionAsync(subscription).GetAwaiter().GetResult().Value;
        WriteObject(updated);
    }

    private static EntityStatus ResolveStatus(string value)
    {
        return value switch
        {
            "Active" => EntityStatus.Active,
            "Disabled" => EntityStatus.Disabled,
            "SendDisabled" => EntityStatus.SendDisabled,
            "ReceiveDisabled" => EntityStatus.ReceiveDisabled,
            _ => throw new ArgumentException($"Unsupported status: {value}")
        };
    }
}
