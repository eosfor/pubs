using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SBSubscription", SupportsShouldProcess = true)]
[OutputType(typeof(SubscriptionProperties))]
public sealed class SetSBSubscriptionCommand : SBEntityTargetCmdletBase
{
    [Parameter]
    [ValidateNotNullOrEmpty]
    [Alias("Name", "TopicName")]
    public string Topic { get; set; } = string.Empty;

    [Parameter(Position = 0)]
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
        var connectionString = ResolveConnectionString();
        var target = ResolveSubscriptionTarget(Topic, Subscription, resolvedConnectionString: connectionString);
        var targetPath = $"{target.Topic}/{target.Subscription}";

        if (!ShouldProcess($"Subscription '{targetPath}' (from {target.Source})", "Update Service Bus subscription"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            var subscription = admin.GetSubscriptionAsync(target.Topic, target.Subscription).GetAwaiter().GetResult().Value;

            Apply(subscription);

            var updated = admin.UpdateSubscriptionAsync(subscription).GetAwaiter().GetResult().Value;
            WriteObject(updated);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "SetSBSubscriptionFailed", ErrorCategory.NotSpecified, targetPath));
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
