using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;
using SBPowerShell;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBSubscription", SupportsShouldProcess = true)]
[OutputType(typeof(SubscriptionProperties))]
public sealed class SetSBSubscriptionCommand : PSCmdlet
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    [Alias("SubscriptionName")]
    public string Subscription { get; set; } = string.Empty;

    [Parameter]
    public bool? EnableBatchedOperations { get; set; }

    [Parameter]
    public bool? DeadLetteringOnMessageExpiration { get; set; }

    [Parameter]
    public bool? EnableDeadLetteringOnFilterEvaluationExceptions { get; set; }

    [Parameter]
    public int? MaxDeliveryCount { get; set; }

    [Parameter]
    public TimeSpan? LockDuration { get; set; }

    [Parameter]
    public TimeSpan? DefaultMessageTimeToLive { get; set; }

    [Parameter]
    public TimeSpan? AutoDeleteOnIdle { get; set; }

    [Parameter]
    public string? ForwardTo { get; set; }

    [Parameter]
    public string? ForwardDeadLetteredMessagesTo { get; set; }

    [Parameter]
    public string? UserMetadata { get; set; }

    protected override void ProcessRecord()
    {
        var target = $"{Topic}/{Subscription}";
        if (!ShouldProcess(target, "Update Service Bus subscription"))
        {
            return;
        }

        try
        {
            var admin = ServiceBusAdminClientFactory.Create(ServiceBusConnectionString);
            var subscription = admin.GetSubscriptionAsync(Topic, Subscription).GetAwaiter().GetResult().Value;

            Apply(subscription);

            var updated = admin.UpdateSubscriptionAsync(subscription).GetAwaiter().GetResult().Value;
            WriteObject(updated);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "SetSBSubscriptionFailed", ErrorCategory.NotSpecified, target));
        }
    }

    private void Apply(SubscriptionProperties subscription)
    {
        if (EnableBatchedOperations.HasValue) subscription.EnableBatchedOperations = EnableBatchedOperations.Value;
        if (DeadLetteringOnMessageExpiration.HasValue) subscription.DeadLetteringOnMessageExpiration = DeadLetteringOnMessageExpiration.Value;
        if (EnableDeadLetteringOnFilterEvaluationExceptions.HasValue) subscription.EnableDeadLetteringOnFilterEvaluationExceptions = EnableDeadLetteringOnFilterEvaluationExceptions.Value;

        if (MaxDeliveryCount.HasValue) subscription.MaxDeliveryCount = MaxDeliveryCount.Value;

        if (LockDuration.HasValue) subscription.LockDuration = LockDuration.Value;
        if (DefaultMessageTimeToLive.HasValue) subscription.DefaultMessageTimeToLive = DefaultMessageTimeToLive.Value;
        if (AutoDeleteOnIdle.HasValue) subscription.AutoDeleteOnIdle = AutoDeleteOnIdle.Value;

        if (MyInvocation.BoundParameters.ContainsKey(nameof(ForwardTo))) subscription.ForwardTo = ForwardTo;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ForwardDeadLetteredMessagesTo))) subscription.ForwardDeadLetteredMessagesTo = ForwardDeadLetteredMessagesTo;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UserMetadata))) subscription.UserMetadata = UserMetadata;
    }
}
