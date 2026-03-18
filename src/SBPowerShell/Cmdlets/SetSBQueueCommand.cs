using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBQueue", SupportsShouldProcess = true)]
[OutputType(typeof(QueueProperties))]
public sealed class SetSBQueueCommand : SBEntityTargetCmdletBase
{
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "QueueName")]
    public string Queue { get; set; } = string.Empty;

    [Parameter]
    public bool? EnableBatchedOperations { get; set; }

    [Parameter]
    public bool? DeadLetteringOnMessageExpiration { get; set; }

    [Parameter]
    public int? MaxSizeInMegabytes { get; set; }

    [Parameter]
    public int? MaxMessageSizeInKilobytes { get; set; }

    [Parameter]
    public int? MaxDeliveryCount { get; set; }

    [Parameter]
    public TimeSpan? LockDuration { get; set; }

    [Parameter]
    public TimeSpan? DefaultMessageTimeToLive { get; set; }

    [Parameter]
    public TimeSpan? AutoDeleteOnIdle { get; set; }

    [Parameter]
    public TimeSpan? DuplicateDetectionHistoryTimeWindow { get; set; }

    [Parameter]
    public string? ForwardTo { get; set; }

    [Parameter]
    public string? ForwardDeadLetteredMessagesTo { get; set; }

    [Parameter]
    public string? UserMetadata { get; set; }

    protected override void ProcessRecord()
    {
        var connectionString = ResolveConnectionString();
        var target = ResolveQueueTarget(Queue);

        if (!ShouldProcess($"Queue '{target.Queue}' (from {target.Source})", "Update Service Bus queue"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            var queue = admin.GetQueueAsync(target.Queue).GetAwaiter().GetResult().Value;

            Apply(queue);

            var updated = admin.UpdateQueueAsync(queue).GetAwaiter().GetResult().Value;
            WriteObject(updated);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "SetSBQueueFailed", ErrorCategory.NotSpecified, target.Queue));
        }
    }

    private void Apply(QueueProperties queue)
    {
        if (EnableBatchedOperations.HasValue) queue.EnableBatchedOperations = EnableBatchedOperations.Value;
        if (DeadLetteringOnMessageExpiration.HasValue) queue.DeadLetteringOnMessageExpiration = DeadLetteringOnMessageExpiration.Value;

        if (MaxSizeInMegabytes.HasValue) queue.MaxSizeInMegabytes = MaxSizeInMegabytes.Value;
        if (MaxMessageSizeInKilobytes.HasValue) queue.MaxMessageSizeInKilobytes = MaxMessageSizeInKilobytes.Value;
        if (MaxDeliveryCount.HasValue) queue.MaxDeliveryCount = MaxDeliveryCount.Value;

        if (LockDuration.HasValue) queue.LockDuration = LockDuration.Value;
        if (DefaultMessageTimeToLive.HasValue) queue.DefaultMessageTimeToLive = DefaultMessageTimeToLive.Value;
        if (AutoDeleteOnIdle.HasValue) queue.AutoDeleteOnIdle = AutoDeleteOnIdle.Value;
        if (DuplicateDetectionHistoryTimeWindow.HasValue) queue.DuplicateDetectionHistoryTimeWindow = DuplicateDetectionHistoryTimeWindow.Value;

        if (MyInvocation.BoundParameters.ContainsKey(nameof(ForwardTo))) queue.ForwardTo = ForwardTo;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ForwardDeadLetteredMessagesTo))) queue.ForwardDeadLetteredMessagesTo = ForwardDeadLetteredMessagesTo;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UserMetadata))) queue.UserMetadata = UserMetadata;
    }
}
