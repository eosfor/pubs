using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBTopic", SupportsShouldProcess = true)]
[OutputType(typeof(TopicProperties))]
public sealed class SetSBTopicCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
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
        if (!ShouldProcess(Topic, "Update Service Bus topic"))
        {
            return;
        }

        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var topic = admin.GetTopicAsync(Topic).GetAwaiter().GetResult().Value;

            Apply(topic);

            var updated = admin.UpdateTopicAsync(topic).GetAwaiter().GetResult().Value;
            WriteObject(updated);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SetSBTopicFailed", ErrorCategory.NotSpecified, Topic));
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
