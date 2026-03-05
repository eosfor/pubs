using System;
using System.Management.Automation;
using Azure.Messaging.ServiceBus.Administration;

namespace SBPowerShell.Cmdlets;

[Cmdlet(VerbsCommon.New, "SBSubscription", SupportsShouldProcess = true)]
[OutputType(typeof(SubscriptionProperties))]
public sealed class NewSBSubscriptionCommand : SBEntityTargetCmdletBase
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
    public bool? RequiresSession { get; set; }

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

    [Parameter]
    public string? SqlFilter { get; set; }

    protected override void ProcessRecord()
    {
        var connectionString = ResolveConnectionString();
        var target = ResolveSubscriptionTarget(Topic, Subscription, resolvedConnectionString: connectionString);
        var targetPath = $"{target.Topic}/{target.Subscription}";

        if (!ShouldProcess($"Subscription '{targetPath}' (from {target.Source})", "Create Service Bus subscription"))
        {
            return;
        }

        try
        {
            var admin = CreateAdminClient(connectionString);
            var options = new CreateSubscriptionOptions(target.Topic, target.Subscription);

            ApplyOptions(options);

            SubscriptionProperties created;
            if (string.IsNullOrWhiteSpace(SqlFilter))
            {
                created = admin.CreateSubscriptionAsync(options).GetAwaiter().GetResult().Value;
            }
            else
            {
                var defaultRule = new CreateRuleOptions("$Default", new SqlRuleFilter(SqlFilter));
                created = admin.CreateSubscriptionAsync(options, defaultRule).GetAwaiter().GetResult().Value;
            }

            WriteObject(created);
        }
        catch (Exception ex)
        {
            if (IsResolverException(ex))
            {
                throw;
            }

            ThrowTerminatingError(new ErrorRecord(ex, "NewSBSubscriptionFailed", ErrorCategory.NotSpecified, targetPath));
        }
    }

    private void ApplyOptions(CreateSubscriptionOptions options)
    {
        if (RequiresSession.HasValue) options.RequiresSession = RequiresSession.Value;
        if (EnableBatchedOperations.HasValue) options.EnableBatchedOperations = EnableBatchedOperations.Value;
        if (DeadLetteringOnMessageExpiration.HasValue) options.DeadLetteringOnMessageExpiration = DeadLetteringOnMessageExpiration.Value;
        if (EnableDeadLetteringOnFilterEvaluationExceptions.HasValue) options.EnableDeadLetteringOnFilterEvaluationExceptions = EnableDeadLetteringOnFilterEvaluationExceptions.Value;

        if (MaxDeliveryCount.HasValue) options.MaxDeliveryCount = MaxDeliveryCount.Value;

        if (LockDuration.HasValue) options.LockDuration = LockDuration.Value;
        if (DefaultMessageTimeToLive.HasValue) options.DefaultMessageTimeToLive = DefaultMessageTimeToLive.Value;
        if (AutoDeleteOnIdle.HasValue) options.AutoDeleteOnIdle = AutoDeleteOnIdle.Value;

        if (!string.IsNullOrWhiteSpace(ForwardTo)) options.ForwardTo = ForwardTo;
        if (!string.IsNullOrWhiteSpace(ForwardDeadLetteredMessagesTo)) options.ForwardDeadLetteredMessagesTo = ForwardDeadLetteredMessagesTo;
        if (UserMetadata is not null) options.UserMetadata = UserMetadata;
    }
}
