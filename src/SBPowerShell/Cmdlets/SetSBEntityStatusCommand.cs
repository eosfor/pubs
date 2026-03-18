using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBEntityStatus", SupportsShouldProcess = true)]
public sealed class SetSBEntityStatusCommand : SBEntityTargetCmdletBase
{
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Queue { get; set; } = string.Empty;

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Subscription { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateSet("Active", "Disabled", "SendDisabled", "ReceiveDisabled")]
    public string Status { get; set; } = "Active";

    protected override void ProcessRecord()
    {
        try
        {
            var connectionString = ResolveConnectionString();
            var status = ResolveStatus(Status);
            var admin = CreateAdminClient(connectionString);
            SetAutoResolvedStatus(admin, status, connectionString);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "SetSBEntityStatusFailed", ErrorCategory.NotSpecified, this));
        }
    }

    private void SetAutoResolvedStatus(ServiceBusAdministrationClient admin, EntityStatus status, string connectionString)
    {
        var target = ResolveQueueTopicOrSubscriptionTarget(
            Queue,
            Topic,
            Subscription,
            resolvedConnectionString: connectionString);

        if (target.Kind == ResolvedEntityKind.Queue)
        {
            if (!ShouldProcess($"Queue '{target.Queue}' (from {target.Source})", $"Set status to {Status}"))
            {
                return;
            }

            var queue = admin.GetQueueAsync(target.Queue).GetAwaiter().GetResult().Value;
            queue.Status = status;
            WriteObject(admin.UpdateQueueAsync(queue).GetAwaiter().GetResult().Value);
            return;
        }

        if (target.Kind == ResolvedEntityKind.Subscription)
        {
            var entityPath = $"{target.Topic}/Subscriptions/{target.Subscription}";
            if (!ShouldProcess($"Subscription '{entityPath}' (from {target.Source})", $"Set status to {Status}"))
            {
                return;
            }

            var subscription = admin.GetSubscriptionAsync(target.Topic, target.Subscription).GetAwaiter().GetResult().Value;
            subscription.Status = status;
            WriteObject(admin.UpdateSubscriptionAsync(subscription).GetAwaiter().GetResult().Value);
            return;
        }

        if (!ShouldProcess($"Topic '{target.Topic}' (from {target.Source})", $"Set status to {Status}"))
        {
            return;
        }

        var topic = admin.GetTopicAsync(target.Topic).GetAwaiter().GetResult().Value;
        topic.Status = status;
        WriteObject(admin.UpdateTopicAsync(topic).GetAwaiter().GetResult().Value);
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
