using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBTopic", SupportsShouldProcess = true)]
[OutputType(typeof(TopicProperties))]
public sealed class NewSBTopicCommand : SBEntityTargetCmdletBase
{
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    public bool? RequiresDuplicateDetection { get; set; }

    [Parameter]
    public bool? EnablePartitioning { get; set; }

    [Parameter]
    public bool? EnableBatchedOperations { get; set; }

    [Parameter]
    public bool? SupportOrdering { get; set; }

    [Parameter]
    public int? MaxSizeInMegabytes { get; set; }

    [Parameter]
    public int? MaxMessageSizeInKilobytes { get; set; }

    [Parameter]
    public TimeSpan? DefaultMessageTimeToLive { get; set; }

    [Parameter]
    public TimeSpan? AutoDeleteOnIdle { get; set; }

    [Parameter]
    public TimeSpan? DuplicateDetectionHistoryTimeWindow { get; set; }

    [Parameter]
    public string? UserMetadata { get; set; }

    protected override void ProcessRecord()
    {
        var connectionString = ResolveConnectionString();
        var target = ResolveTopicTarget(Topic, resolvedConnectionString: connectionString);

        if (!ShouldProcess($"Topic '{target.Topic}' (from {target.Source})", "Create Service Bus topic"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            var options = new CreateTopicOptions(target.Topic);

            ApplyOptions(options);

            var created = admin.CreateTopicAsync(options).GetAwaiter().GetResult().Value;
            WriteObject(created);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "NewSBTopicFailed", ErrorCategory.NotSpecified, target.Topic));
        }
    }

    private void ApplyOptions(CreateTopicOptions options)
    {
        if (RequiresDuplicateDetection.HasValue) options.RequiresDuplicateDetection = RequiresDuplicateDetection.Value;
        if (EnablePartitioning.HasValue) options.EnablePartitioning = EnablePartitioning.Value;
        if (EnableBatchedOperations.HasValue) options.EnableBatchedOperations = EnableBatchedOperations.Value;
        if (SupportOrdering.HasValue) options.SupportOrdering = SupportOrdering.Value;

        if (MaxSizeInMegabytes.HasValue) options.MaxSizeInMegabytes = MaxSizeInMegabytes.Value;
        if (MaxMessageSizeInKilobytes.HasValue) options.MaxMessageSizeInKilobytes = MaxMessageSizeInKilobytes.Value;

        if (DefaultMessageTimeToLive.HasValue) options.DefaultMessageTimeToLive = DefaultMessageTimeToLive.Value;
        if (AutoDeleteOnIdle.HasValue) options.AutoDeleteOnIdle = AutoDeleteOnIdle.Value;
        if (DuplicateDetectionHistoryTimeWindow.HasValue) options.DuplicateDetectionHistoryTimeWindow = DuplicateDetectionHistoryTimeWindow.Value;

        if (UserMetadata is not null) options.UserMetadata = UserMetadata;
    }
}
