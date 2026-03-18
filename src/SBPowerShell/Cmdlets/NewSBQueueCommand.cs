using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBQueue", SupportsShouldProcess = true)]
[OutputType(typeof(QueueProperties))]
public sealed class NewSBQueueCommand : SBEntityTargetCmdletBase
{
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "QueueName")]
    public string Queue { get; set; } = string.Empty;

    [Parameter]
    public bool? RequiresSession { get; set; }

    [Parameter]
    public bool? RequiresDuplicateDetection { get; set; }

    [Parameter]
    public bool? EnablePartitioning { get; set; }

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

        if (!ShouldProcess($"Queue '{target.Queue}' (from {target.Source})", "Create Service Bus queue"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            var options = new CreateQueueOptions(target.Queue);

            ApplyOptions(options);

            var created = admin.CreateQueueAsync(options).GetAwaiter().GetResult().Value;
            WriteObject(created);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "NewSBQueueFailed", ErrorCategory.NotSpecified, target.Queue));
        }
    }

    private void ApplyOptions(CreateQueueOptions options)
    {
        if (RequiresSession.HasValue) options.RequiresSession = RequiresSession.Value;
        if (RequiresDuplicateDetection.HasValue) options.RequiresDuplicateDetection = RequiresDuplicateDetection.Value;
        if (EnablePartitioning.HasValue) options.EnablePartitioning = EnablePartitioning.Value;
        if (EnableBatchedOperations.HasValue) options.EnableBatchedOperations = EnableBatchedOperations.Value;
        if (DeadLetteringOnMessageExpiration.HasValue) options.DeadLetteringOnMessageExpiration = DeadLetteringOnMessageExpiration.Value;

        if (MaxSizeInMegabytes.HasValue) options.MaxSizeInMegabytes = MaxSizeInMegabytes.Value;
        if (MaxMessageSizeInKilobytes.HasValue) options.MaxMessageSizeInKilobytes = MaxMessageSizeInKilobytes.Value;
        if (MaxDeliveryCount.HasValue) options.MaxDeliveryCount = MaxDeliveryCount.Value;

        if (LockDuration.HasValue) options.LockDuration = LockDuration.Value;
        if (DefaultMessageTimeToLive.HasValue) options.DefaultMessageTimeToLive = DefaultMessageTimeToLive.Value;
        if (AutoDeleteOnIdle.HasValue) options.AutoDeleteOnIdle = AutoDeleteOnIdle.Value;
        if (DuplicateDetectionHistoryTimeWindow.HasValue) options.DuplicateDetectionHistoryTimeWindow = DuplicateDetectionHistoryTimeWindow.Value;

        if (!string.IsNullOrWhiteSpace(ForwardTo)) options.ForwardTo = ForwardTo;
        if (!string.IsNullOrWhiteSpace(ForwardDeadLetteredMessagesTo)) options.ForwardDeadLetteredMessagesTo = ForwardDeadLetteredMessagesTo;
        if (UserMetadata is not null) options.UserMetadata = UserMetadata;
    }
}
