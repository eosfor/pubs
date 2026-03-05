using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBTopic", SupportsShouldProcess = true)]
[OutputType(typeof(TopicProperties))]
public sealed class SetSBTopicCommand : SBEntityTargetCmdletBase
{
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter]
    public bool? EnableBatchedOperations { get; set; }

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

        if (!ShouldProcess($"Topic '{target.Topic}' (from {target.Source})", "Update Service Bus topic"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            var topic = admin.GetTopicAsync(target.Topic).GetAwaiter().GetResult().Value;

            Apply(topic);

            var updated = admin.UpdateTopicAsync(topic).GetAwaiter().GetResult().Value;
            WriteObject(updated);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "SetSBTopicFailed", ErrorCategory.NotSpecified, target.Topic));
        }
    }

    private void Apply(TopicProperties topic)
    {
        if (EnableBatchedOperations.HasValue) topic.EnableBatchedOperations = EnableBatchedOperations.Value;

        if (MaxSizeInMegabytes.HasValue) topic.MaxSizeInMegabytes = MaxSizeInMegabytes.Value;
        if (MaxMessageSizeInKilobytes.HasValue) topic.MaxMessageSizeInKilobytes = MaxMessageSizeInKilobytes.Value;

        if (DefaultMessageTimeToLive.HasValue) topic.DefaultMessageTimeToLive = DefaultMessageTimeToLive.Value;
        if (AutoDeleteOnIdle.HasValue) topic.AutoDeleteOnIdle = AutoDeleteOnIdle.Value;
        if (DuplicateDetectionHistoryTimeWindow.HasValue) topic.DuplicateDetectionHistoryTimeWindow = DuplicateDetectionHistoryTimeWindow.Value;

        if (MyInvocation.BoundParameters.ContainsKey(nameof(UserMetadata))) topic.UserMetadata = UserMetadata;
    }
}
